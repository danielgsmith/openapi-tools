using Microsoft.OpenApi.Models;
using OpenApiTools.Models;

namespace OpenApiTools.Services;

public interface IOpenApiMerger
{
    MergeResult Merge(MergeConfiguration config, IReadOnlyList<(SourceConfiguration Source, OpenApiDocument Document)> sources);
}

public sealed class OpenApiMerger : IOpenApiMerger
{
    private sealed class SourceComponentMaps
    {
        private readonly Dictionary<ReferenceType, Dictionary<string, string>> _maps = new();

        public SourceComponentMaps(string sourceName)
        {
            SourceName = sourceName;
        }

        public string SourceName { get; }

        public void Register(ReferenceType type, string sourceName, string mergedName)
        {
            if (!_maps.TryGetValue(type, out var map))
            {
                map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _maps[type] = map;
            }

            map[sourceName] = mergedName;
        }

        public string Rewrite(ReferenceType type, string sourceName)
        {
            if (_maps.TryGetValue(type, out var map) && map.TryGetValue(sourceName, out var mergedName))
                return mergedName;

            return sourceName;
        }
    }

    public MergeResult Merge(
        MergeConfiguration config,
        IReadOnlyList<(SourceConfiguration Source, OpenApiDocument Document)> sources)
    {
        var warnings = new List<MergeWarning>();
        var merged = new OpenApiDocument
        {
            Info = new OpenApiInfo
            {
                Title = config.Info.Title,
                Version = config.Info.Version,
                Description = config.Info.Description,
            },
            Servers = config.Servers.Select(s => new OpenApiServer
            {
                Url = s.Url,
                Description = s.Description,
            }).ToList(),
            Paths = new OpenApiPaths(),
            Components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, OpenApiSchema>(),
                Responses = new Dictionary<string, OpenApiResponse>(),
                Parameters = new Dictionary<string, OpenApiParameter>(),
                Examples = new Dictionary<string, OpenApiExample>(),
                RequestBodies = new Dictionary<string, OpenApiRequestBody>(),
                Headers = new Dictionary<string, OpenApiHeader>(),
                SecuritySchemes = new Dictionary<string, OpenApiSecurityScheme>(),
                Links = new Dictionary<string, OpenApiLink>(),
                Callbacks = new Dictionary<string, OpenApiCallback>(),
            },
            Tags = [],
            SecurityRequirements = [],
        };

        var seenOperationIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenPaths = new HashSet<string>(StringComparer.Ordinal);

        var schemaRegistry = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (sourceConfig, doc) in sources)
        {
            var sourceName = sourceConfig.Name ?? Path.GetFileNameWithoutExtension(sourceConfig.Path);
            var pathPrefix = NormalizePathPrefix(sourceConfig.PathPrefix);
            var opIdPrefix = sourceConfig.OperationIdPrefix ?? "";
            var maps = new SourceComponentMaps(sourceName);

            MergeSchemas(doc, sourceName, config.SchemaConflict, schemaRegistry, maps, warnings, merged);
            MergeNamedComponents(doc.Components?.Parameters, merged.Components.Parameters!, ReferenceType.Parameter, "parameter", sourceName, maps, warnings, CloneParameterDefinition);
            MergeNamedComponents(doc.Components?.Responses, merged.Components.Responses!, ReferenceType.Response, "response", sourceName, maps, warnings, CloneResponseDefinition);
            MergeNamedComponents(doc.Components?.RequestBodies, merged.Components.RequestBodies!, ReferenceType.RequestBody, "request body", sourceName, maps, warnings, CloneRequestBodyDefinition);
            MergeNamedComponents(doc.Components?.Headers, merged.Components.Headers!, ReferenceType.Header, "header", sourceName, maps, warnings, CloneHeaderDefinition);
            MergeNamedComponents(doc.Components?.Examples, merged.Components.Examples!, ReferenceType.Example, "example", sourceName, maps, warnings, CloneExampleDefinition);
            MergeNamedComponents(doc.Components?.Links, merged.Components.Links!, ReferenceType.Link, "link", sourceName, maps, warnings, CloneLinkDefinition);
            MergeNamedComponents(doc.Components?.Callbacks, merged.Components.Callbacks!, ReferenceType.Callback, "callback", sourceName, maps, warnings, CloneCallbackDefinition);
            MergeSecuritySchemes(doc, sourceName, maps, merged, warnings);
            MergePaths(doc, sourceName, pathPrefix, opIdPrefix, seenPaths, seenOperationIds, merged, warnings, maps);
            MergeTags(doc, merged);
            MergeDocumentSecurity(doc, merged, maps);
        }

        return new MergeResult
        {
            Document = merged,
            Warnings = warnings,
        };
    }

    private static string NormalizePathPrefix(string? prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix)) return "";
        if (!prefix.StartsWith('/')) prefix = "/" + prefix;
        return prefix.TrimEnd('/');
    }

    private static void MergeSchemas(
        OpenApiDocument doc,
        string sourceName,
        SchemaConflictStrategy strategy,
        Dictionary<string, string> registry,
        SourceComponentMaps maps,
        List<MergeWarning> warnings,
        OpenApiDocument merged)
    {
        if (doc.Components?.Schemas is null) return;

        foreach (var (name, schema) in doc.Components.Schemas)
        {
            string finalName;

            if (registry.TryGetValue(name, out var existingOwner))
            {
                switch (strategy)
                {
                    case SchemaConflictStrategy.Fail:
                        warnings.Add(new MergeWarning(
                            "ERROR: Schema conflict on '" + name + "' from source '" + sourceName +
                            "' (already defined by '" + existingOwner + "'). Use rename or first-wins strategy.",
                            sourceName));
                        continue;

                    case SchemaConflictStrategy.FirstWins:
                        warnings.Add(new MergeWarning(
                            "Schema '" + name + "' from '" + sourceName + "' ignored (first-wins, already defined by '" + existingOwner + "').",
                            sourceName));
                        maps.Register(ReferenceType.Schema, name, name);
                        continue;

                    case SchemaConflictStrategy.Rename:
                        finalName = sourceName.Replace(" ", "_").Replace("-", "_") + "_" + name;
                        finalName = EnsureUniqueSchemaName(finalName, registry);
                        registry[finalName] = sourceName;
                        maps.Register(ReferenceType.Schema, name, finalName);
                        warnings.Add(new MergeWarning(
                            "Schema '" + name + "' from '" + sourceName + "' renamed to '" + finalName + "'.",
                            sourceName));
                        break;

                    default:
                        finalName = name;
                        break;
                }
            }
            else
            {
                finalName = name;
                registry[name] = sourceName;
                maps.Register(ReferenceType.Schema, name, name);
            }

            merged.Components.Schemas![finalName] = CloneSchemaDefinition(schema, maps, name);
        }
    }

    private static void MergeNamedComponents<T>(
        IDictionary<string, T>? source,
        IDictionary<string, T> target,
        ReferenceType referenceType,
        string label,
        string sourceName,
        SourceComponentMaps maps,
        List<MergeWarning> warnings,
        Func<T, SourceComponentMaps, string, T> cloneDefinition)
        where T : class
    {
        if (source is null) return;

        foreach (var (name, component) in source)
        {
            maps.Register(referenceType, name, name);

            if (target.ContainsKey(name))
            {
                warnings.Add(new MergeWarning(
                    "Duplicate " + label + " '" + name + "' from '" + sourceName + "'; keeping first definition.",
                    sourceName));
                continue;
            }

            target[name] = cloneDefinition(component, maps, name);
        }
    }

    private static string EnsureUniqueSchemaName(string name, Dictionary<string, string> registry)
    {
        if (!registry.ContainsKey(name)) return name;
        var i = 1;
        while (registry.ContainsKey(name + "_" + i)) i++;
        return name + "_" + i;
    }

    private static void MergeSecuritySchemes(
        OpenApiDocument doc,
        string sourceName,
        SourceComponentMaps maps,
        OpenApiDocument merged,
        List<MergeWarning> warnings)
    {
        if (doc.Components?.SecuritySchemes is null) return;

        foreach (var (name, scheme) in doc.Components.SecuritySchemes)
        {
            maps.Register(ReferenceType.SecurityScheme, name, name);

            if (merged.Components.SecuritySchemes!.ContainsKey(name))
            {
                warnings.Add(new MergeWarning(
                    "Security scheme '" + name + "' from '" + sourceName + "' already exists; keeping first definition.",
                    sourceName));
                continue;
            }
            merged.Components.SecuritySchemes[name] = CloneSecuritySchemeDefinition(scheme, maps, name);
        }
    }

    private static void MergePaths(
        OpenApiDocument doc,
        string sourceName,
        string pathPrefix,
        string opIdPrefix,
        HashSet<string> seenPaths,
        HashSet<string> seenOperationIds,
        OpenApiDocument merged,
        List<MergeWarning> warnings,
        SourceComponentMaps maps)
    {
        if (doc.Paths is null) return;

        foreach (var (path, item) in doc.Paths)
        {
            var newPath = pathPrefix + path;

            if (seenPaths.Contains(newPath))
            {
                warnings.Add(new MergeWarning(
                    "Path '" + newPath + "' from '" + sourceName + "' already exists; skipping.",
                    sourceName));
                continue;
            }
            seenPaths.Add(newPath);

            var newItem = new OpenApiPathItem
            {
                Summary = item.Summary,
                Description = item.Description,
                Servers = item.Servers?.Select(CloneServer).ToList(),
                Parameters = item.Parameters?.Select(p => CloneParameterReferenceOrInline(p, maps)).ToList(),
            };

            foreach (var (method, op) in item.Operations)
            {
                var newOp = CloneOperation(op, opIdPrefix, maps);

                if (!string.IsNullOrEmpty(newOp.OperationId) && !seenOperationIds.Add(newOp.OperationId))
                {
                    warnings.Add(new MergeWarning(
                        "Duplicate operationId '" + newOp.OperationId + "' from '" + sourceName + "'.",
                        sourceName));
                }

                newItem.Operations[method] = newOp;
            }

            merged.Paths[newPath] = newItem;
        }
    }

    private static void MergeTags(OpenApiDocument doc, OpenApiDocument merged)
    {
        if (doc.Tags is null) return;
        merged.Tags ??= [];
        var existing = merged.Tags.Select(t => t.Name ?? "").ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var tag in doc.Tags)
        {
            if (existing.Add(tag.Name ?? ""))
            {
                merged.Tags.Add(new OpenApiTag
                {
                    Name = tag.Name,
                    Description = tag.Description,
                    ExternalDocs = tag.ExternalDocs is null ? null : new OpenApiExternalDocs
                    {
                        Url = tag.ExternalDocs.Url,
                        Description = tag.ExternalDocs.Description,
                    },
                });
            }
        }
    }

    private static void MergeDocumentSecurity(OpenApiDocument doc, OpenApiDocument merged, SourceComponentMaps maps)
    {
        if (doc.SecurityRequirements is null) return;
        merged.SecurityRequirements ??= [];

        foreach (var requirement in doc.SecurityRequirements)
            merged.SecurityRequirements.Add(CloneSecurityRequirement(requirement, maps));
    }

    private static OpenApiOperation CloneOperation(OpenApiOperation op, string opIdPrefix, SourceComponentMaps maps)
    {
        return new OpenApiOperation
        {
            Tags = op.Tags?.Select(t => new OpenApiTag { Name = t.Name }).ToList(),
            Summary = op.Summary,
            Description = op.Description,
            ExternalDocs = op.ExternalDocs is null ? null : new OpenApiExternalDocs
            {
                Url = op.ExternalDocs.Url,
                Description = op.ExternalDocs.Description,
            },
            OperationId = string.IsNullOrEmpty(op.OperationId) ? op.OperationId : opIdPrefix + op.OperationId,
            Parameters = op.Parameters?.Select(p => CloneParameterReferenceOrInline(p, maps)).ToList(),
            RequestBody = op.RequestBody is null ? null : CloneRequestBodyReferenceOrInline(op.RequestBody, maps),
            Responses = CloneResponses(op.Responses, maps),
            Deprecated = op.Deprecated,
            Security = op.Security?.Select(req => CloneSecurityRequirement(req, maps)).ToList(),
            Servers = op.Servers?.Select(CloneServer).ToList(),
        };
    }

    private static OpenApiParameter CloneParameterDefinition(OpenApiParameter p, SourceComponentMaps maps, string definitionName)
    {
        return new OpenApiParameter
        {
            Name = p.Name,
            In = p.In,
            Description = p.Description,
            Required = p.Required,
            Deprecated = p.Deprecated,
            AllowEmptyValue = p.AllowEmptyValue,
            Style = p.Style,
            Explode = p.Explode,
            AllowReserved = p.AllowReserved,
            Schema = p.Schema is null ? null : CloneSchemaReferenceOrInline(p.Schema, maps),
            Example = p.Example,
            Examples = p.Examples?.ToDictionary(e => e.Key, e => CloneExampleReferenceOrInline(e.Value, maps)),
            Content = p.Content?.ToDictionary(c => c.Key, c => CloneMediaType(c.Value, maps)),
        };
    }

    private static OpenApiParameter CloneParameterReferenceOrInline(OpenApiParameter p, SourceComponentMaps maps)
    {
        if (p.Reference is not null && p.Reference.Type == ReferenceType.Parameter)
            return new OpenApiParameter { Reference = RewriteReference(p.Reference, maps), UnresolvedReference = true };

        return CloneParameterDefinition(p, maps, p.Reference?.Id ?? p.Name ?? string.Empty);
    }

    private static OpenApiRequestBody CloneRequestBodyDefinition(OpenApiRequestBody b, SourceComponentMaps maps, string definitionName)
    {
        return new OpenApiRequestBody
        {
            Description = b.Description,
            Required = b.Required,
            Content = b.Content?.ToDictionary(c => c.Key, c => CloneMediaType(c.Value, maps)),
        };
    }

    private static OpenApiRequestBody CloneRequestBodyReferenceOrInline(OpenApiRequestBody b, SourceComponentMaps maps)
    {
        if (b.Reference is not null && b.Reference.Type == ReferenceType.RequestBody)
            return new OpenApiRequestBody { Reference = RewriteReference(b.Reference, maps), UnresolvedReference = true };

        return CloneRequestBodyDefinition(b, maps, b.Reference?.Id ?? string.Empty);
    }

    private static OpenApiResponses CloneResponses(OpenApiResponses? responses, SourceComponentMaps maps)
    {
        var cloned = new OpenApiResponses();
        if (responses is null) return cloned;

        foreach (var (code, resp) in responses)
        {
            cloned[code] = CloneResponseReferenceOrInline(resp, maps);
        }
        return cloned;
    }

    private static OpenApiResponse CloneResponseDefinition(OpenApiResponse r, SourceComponentMaps maps, string definitionName)
    {
        return new OpenApiResponse
        {
            Description = r.Description,
            Headers = r.Headers?.ToDictionary(h => h.Key, h => CloneHeaderReferenceOrInline(h.Value, maps)),
            Content = r.Content?.ToDictionary(c => c.Key, c => CloneMediaType(c.Value, maps)),
            Links = r.Links?.ToDictionary(l => l.Key, l => CloneLinkReferenceOrInline(l.Value, maps)),
        };
    }

    private static OpenApiResponse CloneResponseReferenceOrInline(OpenApiResponse r, SourceComponentMaps maps)
    {
        if (r.Reference is not null && r.Reference.Type == ReferenceType.Response)
            return new OpenApiResponse { Reference = RewriteReference(r.Reference, maps), UnresolvedReference = true };

        return CloneResponseDefinition(r, maps, r.Reference?.Id ?? string.Empty);
    }

    private static OpenApiHeader CloneHeaderDefinition(OpenApiHeader h, SourceComponentMaps maps, string definitionName)
    {
        return new OpenApiHeader
        {
            Description = h.Description,
            Required = h.Required,
            Deprecated = h.Deprecated,
            Schema = h.Schema is null ? null : CloneSchemaReferenceOrInline(h.Schema, maps),
            Example = h.Example,
            Examples = h.Examples?.ToDictionary(e => e.Key, e => CloneExampleReferenceOrInline(e.Value, maps)),
            Content = h.Content?.ToDictionary(c => c.Key, c => CloneMediaType(c.Value, maps)),
        };
    }

    private static OpenApiHeader CloneHeaderReferenceOrInline(OpenApiHeader h, SourceComponentMaps maps)
    {
        if (h.Reference is not null && h.Reference.Type == ReferenceType.Header)
            return new OpenApiHeader { Reference = RewriteReference(h.Reference, maps), UnresolvedReference = true };

        return CloneHeaderDefinition(h, maps, h.Reference?.Id ?? string.Empty);
    }

    private static OpenApiMediaType CloneMediaType(OpenApiMediaType m, SourceComponentMaps maps)
    {
        return new OpenApiMediaType
        {
            Schema = m.Schema is null ? null : CloneSchemaReferenceOrInline(m.Schema, maps),
            Example = m.Example,
            Examples = m.Examples?.ToDictionary(e => e.Key, e => CloneExampleReferenceOrInline(e.Value, maps)),
            Encoding = m.Encoding?.ToDictionary(e => e.Key, e => new OpenApiEncoding
            {
                ContentType = e.Value.ContentType,
                Style = e.Value.Style,
                Explode = e.Value.Explode,
                AllowReserved = e.Value.AllowReserved,
                Headers = e.Value.Headers?.ToDictionary(h => h.Key, h => CloneHeaderReferenceOrInline(h.Value, maps)),
            }),
        };
    }

    private static OpenApiSchema CloneSchemaDefinition(OpenApiSchema s, SourceComponentMaps maps, string definitionName)
    {
        return new OpenApiSchema
        {
            Title = s.Title,
            Type = s.Type,
            Format = s.Format,
            Description = s.Description,
            Maximum = s.Maximum,
            ExclusiveMaximum = s.ExclusiveMaximum,
            Minimum = s.Minimum,
            ExclusiveMinimum = s.ExclusiveMinimum,
            MaxLength = s.MaxLength,
            MinLength = s.MinLength,
            Pattern = s.Pattern,
            MultipleOf = s.MultipleOf,
            Default = s.Default,
            ReadOnly = s.ReadOnly,
            WriteOnly = s.WriteOnly,
            AllOf = s.AllOf?.Select(x => CloneSchemaReferenceOrInline(x, maps)).ToList(),
            OneOf = s.OneOf?.Select(x => CloneSchemaReferenceOrInline(x, maps)).ToList(),
            AnyOf = s.AnyOf?.Select(x => CloneSchemaReferenceOrInline(x, maps)).ToList(),
            Not = s.Not is null ? null : CloneSchemaReferenceOrInline(s.Not, maps),
            Required = s.Required is null ? null : new HashSet<string>(s.Required),
            Items = s.Items is null ? null : CloneSchemaReferenceOrInline(s.Items, maps),
            MaxItems = s.MaxItems,
            MinItems = s.MinItems,
            UniqueItems = s.UniqueItems,
            Properties = s.Properties?.ToDictionary(p => p.Key, p => CloneSchemaReferenceOrInline(p.Value, maps)),
            MaxProperties = s.MaxProperties,
            MinProperties = s.MinProperties,
            AdditionalPropertiesAllowed = s.AdditionalPropertiesAllowed,
            AdditionalProperties = s.AdditionalProperties is null ? null : CloneSchemaReferenceOrInline(s.AdditionalProperties, maps),
            Discriminator = s.Discriminator is null ? null : new OpenApiDiscriminator
            {
                PropertyName = s.Discriminator.PropertyName,
                Mapping = s.Discriminator.Mapping is null ? null : new Dictionary<string, string>(s.Discriminator.Mapping),
            },
            Example = s.Example,
            Enum = s.Enum?.ToList(),
            Nullable = s.Nullable,
            Deprecated = s.Deprecated,
        };
    }

    private static OpenApiSchema CloneSchemaReferenceOrInline(OpenApiSchema s, SourceComponentMaps maps)
    {
        if (s.Reference is not null && s.Reference.Type == ReferenceType.Schema)
            return new OpenApiSchema { Reference = RewriteReference(s.Reference, maps), UnresolvedReference = true };

        return CloneSchemaDefinition(s, maps, s.Reference?.Id ?? string.Empty);
    }

    private static OpenApiExample CloneExampleDefinition(OpenApiExample e, SourceComponentMaps maps, string definitionName)
    {
        return new OpenApiExample
        {
            Summary = e.Summary,
            Description = e.Description,
            Value = e.Value,
            ExternalValue = e.ExternalValue,
        };
    }

    private static OpenApiExample CloneExampleReferenceOrInline(OpenApiExample e, SourceComponentMaps maps)
    {
        if (e.Reference is not null && e.Reference.Type == ReferenceType.Example)
            return new OpenApiExample { Reference = RewriteReference(e.Reference, maps), UnresolvedReference = true };

        return CloneExampleDefinition(e, maps, e.Reference?.Id ?? string.Empty);
    }

    private static OpenApiLink CloneLinkDefinition(OpenApiLink l, SourceComponentMaps maps, string definitionName)
    {
        return new OpenApiLink
        {
            OperationId = l.OperationId,
            OperationRef = l.OperationRef,
            Description = l.Description,
            Parameters = l.Parameters is null ? null : new Dictionary<string, RuntimeExpressionAnyWrapper>(l.Parameters),
            RequestBody = l.RequestBody,
            Server = l.Server is null ? null : CloneServer(l.Server),
        };
    }

    private static OpenApiLink CloneLinkReferenceOrInline(OpenApiLink l, SourceComponentMaps maps)
    {
        if (l.Reference is not null && l.Reference.Type == ReferenceType.Link)
            return new OpenApiLink { Reference = RewriteReference(l.Reference, maps), UnresolvedReference = true };

        return CloneLinkDefinition(l, maps, l.Reference?.Id ?? string.Empty);
    }

    private static OpenApiCallback CloneCallbackDefinition(OpenApiCallback c, SourceComponentMaps maps, string definitionName)
    {
        var callback = new OpenApiCallback();
        foreach (var (expression, pathItem) in c.PathItems)
            callback.PathItems[expression] = ClonePathItem(pathItem, maps, "");
        return callback;
    }

    private static OpenApiCallback CloneCallbackReferenceOrInline(OpenApiCallback c, SourceComponentMaps maps)
    {
        if (c.Reference is not null && c.Reference.Type == ReferenceType.Callback)
            return new OpenApiCallback { Reference = RewriteReference(c.Reference, maps), UnresolvedReference = true };

        return CloneCallbackDefinition(c, maps, c.Reference?.Id ?? string.Empty);
    }

    private static OpenApiPathItem ClonePathItem(OpenApiPathItem item, SourceComponentMaps maps, string opIdPrefix)
    {
        var clone = new OpenApiPathItem
        {
            Summary = item.Summary,
            Description = item.Description,
            Servers = item.Servers?.Select(CloneServer).ToList(),
            Parameters = item.Parameters?.Select(p => CloneParameterReferenceOrInline(p, maps)).ToList(),
        };

        foreach (var (method, op) in item.Operations)
            clone.Operations[method] = CloneOperation(op, opIdPrefix, maps);

        return clone;
    }

    private static OpenApiSecurityScheme CloneSecuritySchemeDefinition(OpenApiSecurityScheme s, SourceComponentMaps maps, string definitionName)
    {
        return new OpenApiSecurityScheme
        {
            Type = s.Type,
            Description = s.Description,
            Name = s.Name,
            In = s.In,
            Scheme = s.Scheme,
            BearerFormat = s.BearerFormat,
            Flows = s.Flows is null ? null : new OpenApiOAuthFlows
            {
                Implicit = s.Flows.Implicit is null ? null : CloneOAuthFlow(s.Flows.Implicit),
                Password = s.Flows.Password is null ? null : CloneOAuthFlow(s.Flows.Password),
                ClientCredentials = s.Flows.ClientCredentials is null ? null : CloneOAuthFlow(s.Flows.ClientCredentials),
                AuthorizationCode = s.Flows.AuthorizationCode is null ? null : CloneOAuthFlow(s.Flows.AuthorizationCode),
            },
            OpenIdConnectUrl = s.OpenIdConnectUrl,
        };
    }

    private static OpenApiOAuthFlow CloneOAuthFlow(OpenApiOAuthFlow f)
    {
        return new OpenApiOAuthFlow
        {
            AuthorizationUrl = f.AuthorizationUrl,
            TokenUrl = f.TokenUrl,
            RefreshUrl = f.RefreshUrl,
            Scopes = f.Scopes is null ? null : new Dictionary<string, string>(f.Scopes),
        };
    }

    private static OpenApiSecurityRequirement CloneSecurityRequirement(OpenApiSecurityRequirement req, SourceComponentMaps maps)
    {
        var cloned = new OpenApiSecurityRequirement();
        foreach (var (scheme, scopes) in req)
        {
            var rewrittenScheme = scheme.Reference is not null
                ? new OpenApiSecurityScheme { Reference = RewriteReference(scheme.Reference, maps), UnresolvedReference = true }
                : CloneSecuritySchemeDefinition(scheme, maps, scheme.Reference?.Id ?? string.Empty);
            cloned[rewrittenScheme] = scopes?.ToList() ?? [];
        }
        return cloned;
    }

    private static OpenApiServer CloneServer(OpenApiServer s)
    {
        return new OpenApiServer
        {
            Url = s.Url,
            Description = s.Description,
            Variables = s.Variables?.ToDictionary(v => v.Key, v => new OpenApiServerVariable
            {
                Default = v.Value.Default,
                Description = v.Value.Description,
                Enum = v.Value.Enum?.ToList(),
            }),
        };
    }

    private static OpenApiReference RewriteReference(OpenApiReference reference, SourceComponentMaps maps)
    {
        var id = reference.Id;
        if (reference.Type is not null)
            id = maps.Rewrite(reference.Type.Value, reference.Id);

        return new OpenApiReference
        {
            Id = id,
            Type = reference.Type,
            ExternalResource = reference.ExternalResource,
        };
    }
}
