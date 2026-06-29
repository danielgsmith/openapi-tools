using Microsoft.OpenApi.Models;
using OpenApiTools.Models;

namespace OpenApiTools.Services;

public interface IOpenApiMerger
{
    MergeResult Merge(MergeConfiguration config, IReadOnlyList<(SourceConfiguration Source, OpenApiDocument Document)> sources);
}

public sealed class OpenApiMerger : IOpenApiMerger
{
    private readonly IOpenApiComponentComparer _componentComparer;
    private readonly IOpenApiConflictDetector _conflictDetector;

    public OpenApiMerger()
        : this(new OpenApiComponentComparer(), new OpenApiConflictDetector())
    {
    }

    public OpenApiMerger(IOpenApiComponentComparer componentComparer, IOpenApiConflictDetector conflictDetector)
    {
        _componentComparer = componentComparer;
        _conflictDetector = conflictDetector;
    }

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
        var diagnostics = new List<MergeDiagnostic>();
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

        var schemaRegistry = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var parameterOwners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var responseOwners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var requestBodyOwners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var headerOwners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var exampleOwners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var linkOwners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var callbackOwners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var securitySchemeOwners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (sourceConfig, doc) in sources)
        {
            var sourceName = sourceConfig.Name ?? Path.GetFileNameWithoutExtension(sourceConfig.Path);
            var pathPrefix = NormalizePathPrefix(sourceConfig.PathPrefix);
            var opIdPrefix = sourceConfig.OperationIdPrefix ?? "";
            var maps = new SourceComponentMaps(sourceName);

            MergeSchemas(doc, sourceName, config.Conflicts.Schemas, schemaRegistry, maps, diagnostics, merged);
            MergeNamedComponents(doc.Components?.Parameters, merged.Components.Parameters!, parameterOwners, config.Conflicts.Parameters, ReferenceType.Parameter, "parameter", sourceName, maps, diagnostics, merged, CloneParameterDefinition, _componentComparer.AreEquivalent);
            MergeNamedComponents(doc.Components?.Responses, merged.Components.Responses!, responseOwners, config.Conflicts.Responses, ReferenceType.Response, "response", sourceName, maps, diagnostics, merged, CloneResponseDefinition, _componentComparer.AreEquivalent);
            MergeNamedComponents(doc.Components?.RequestBodies, merged.Components.RequestBodies!, requestBodyOwners, config.Conflicts.RequestBodies, ReferenceType.RequestBody, "request body", sourceName, maps, diagnostics, merged, CloneRequestBodyDefinition, _componentComparer.AreEquivalent);
            MergeNamedComponents(doc.Components?.Headers, merged.Components.Headers!, headerOwners, config.Conflicts.Headers, ReferenceType.Header, "header", sourceName, maps, diagnostics, merged, CloneHeaderDefinition, _componentComparer.AreEquivalent);
            MergeNamedComponents(doc.Components?.Examples, merged.Components.Examples!, exampleOwners, config.Conflicts.Examples, ReferenceType.Example, "example", sourceName, maps, diagnostics, merged, CloneExampleDefinition, _componentComparer.AreEquivalent);
            MergeNamedComponents(doc.Components?.Links, merged.Components.Links!, linkOwners, config.Conflicts.Links, ReferenceType.Link, "link", sourceName, maps, diagnostics, merged, CloneLinkDefinition, _componentComparer.AreEquivalent);
            MergeNamedComponents(doc.Components?.Callbacks, merged.Components.Callbacks!, callbackOwners, config.Conflicts.Callbacks, ReferenceType.Callback, "callback", sourceName, maps, diagnostics, merged, CloneCallbackDefinition, _componentComparer.AreEquivalent);
            MergeSecuritySchemes(doc, sourceName, maps, merged, diagnostics, securitySchemeOwners, config.Conflicts.SecuritySchemes);
            MergePaths(doc, sourceName, pathPrefix, opIdPrefix, seenOperationIds, merged, diagnostics, maps);
            MergeTags(doc, merged);
            MergeDocumentSecurity(doc, merged, maps);
        }

        return new MergeResult
        {
            Document = merged,
            Diagnostics = diagnostics,
        };
    }

    private static string NormalizePathPrefix(string? prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix)) return "";
        if (!prefix.StartsWith('/')) prefix = "/" + prefix;
        return prefix.TrimEnd('/');
    }

    private void MergeSchemas(
        OpenApiDocument doc,
        string sourceName,
        MergeComponentConflictPolicy policy,
        Dictionary<string, string> registry,
        SourceComponentMaps maps,
        List<MergeDiagnostic> diagnostics,
        OpenApiDocument merged)
    {
        if (doc.Components?.Schemas is null) return;

        foreach (var (name, schema) in doc.Components.Schemas)
        {
            var conflict = _conflictDetector.Detect(
                "schema",
                name,
                sourceName,
                merged.Components.Schemas!,
                registry,
                _componentComparer.AreEquivalent,
                schema);

            string finalName;

            if (conflict.Kind == MergeConflictKind.Unique)
            {
                finalName = name;
                registry[name] = sourceName;
                maps.Register(ReferenceType.Schema, name, name);
            }
            else if (conflict.Kind == MergeConflictKind.IdenticalDuplicate)
            {
                maps.Register(ReferenceType.Schema, name, name);

                if (policy.Identical == MergeDuplicateHandling.Fail)
                {
                    diagnostics.Add(new MergeDiagnostic(
                        MergeDiagnosticSeverity.Error,
                        "Identical schema '" + name + "' from '" + sourceName + "' duplicates the definition from '" + conflict.ExistingOwner + "'.",
                        sourceName));
                }
                else if (policy.Identical == MergeDuplicateHandling.WarnAndDedupe)
                {
                    diagnostics.Add(new MergeDiagnostic(
                        MergeDiagnosticSeverity.Warning,
                        "Identical schema '" + name + "' from '" + sourceName + "' deduplicated against '" + conflict.ExistingOwner + "'.",
                        sourceName));
                }

                continue;
            }
            else
            {
                switch (policy.Conflict)
                {
                    case MergeConflictResolution.Fail:
                        diagnostics.Add(new MergeDiagnostic(
                            MergeDiagnosticSeverity.Error,
                            "Schema conflict on '" + name + "' from source '" + sourceName +
                            "' (already defined by '" + conflict.ExistingOwner + "').",
                            sourceName));
                        maps.Register(ReferenceType.Schema, name, name);
                        continue;

                    case MergeConflictResolution.KeepExisting:
                        diagnostics.Add(new MergeDiagnostic(
                            MergeDiagnosticSeverity.Warning,
                            "Schema '" + name + "' from '" + sourceName + "' ignored; keeping existing definition from '" + conflict.ExistingOwner + "'.",
                            sourceName));
                        maps.Register(ReferenceType.Schema, name, name);
                        continue;

                    case MergeConflictResolution.KeepIncoming:
                        diagnostics.Add(new MergeDiagnostic(
                            MergeDiagnosticSeverity.Warning,
                            "Schema '" + name + "' from '" + sourceName + "' replaced the existing definition from '" + conflict.ExistingOwner + "'.",
                            sourceName));
                        finalName = name;
                        registry[name] = sourceName;
                        maps.Register(ReferenceType.Schema, name, name);
                        break;

                    case MergeConflictResolution.RenameExisting:
                        var renamedExistingSchema = RenameExistingComponent(
                            merged,
                            merged.Components.Schemas!,
                            registry,
                            ReferenceType.Schema,
                            name,
                            conflict.ExistingOwner ?? "existing");
                        diagnostics.Add(new MergeDiagnostic(
                            MergeDiagnosticSeverity.Warning,
                            "Existing schema '" + name + "' from '" + conflict.ExistingOwner + "' renamed to '" + renamedExistingSchema + "'; incoming definition from '" + sourceName + "' kept as '" + name + "'.",
                            sourceName));
                        finalName = name;
                        registry[name] = sourceName;
                        maps.Register(ReferenceType.Schema, name, name);
                        break;

                    case MergeConflictResolution.RenameIncoming:
                        finalName = BuildScopedName(sourceName, name, registry);
                        registry[finalName] = sourceName;
                        maps.Register(ReferenceType.Schema, name, finalName);
                        diagnostics.Add(new MergeDiagnostic(
                            MergeDiagnosticSeverity.Warning,
                            "Schema '" + name + "' from '" + sourceName + "' renamed to '" + finalName + "'.",
                            sourceName));
                        break;

                    case MergeConflictResolution.RenameBoth:
                        var renamedExistingSchemaForBoth = RenameExistingComponent(
                            merged,
                            merged.Components.Schemas!,
                            registry,
                            ReferenceType.Schema,
                            name,
                            conflict.ExistingOwner ?? "existing");
                        finalName = BuildScopedName(sourceName, name, registry);
                        registry[finalName] = sourceName;
                        maps.Register(ReferenceType.Schema, name, finalName);
                        diagnostics.Add(new MergeDiagnostic(
                            MergeDiagnosticSeverity.Warning,
                            "Schema '" + name + "' from '" + conflict.ExistingOwner + "' renamed to '" + renamedExistingSchemaForBoth + "', and incoming schema from '" + sourceName + "' renamed to '" + finalName + "'.",
                            sourceName));
                        break;

                    default:
                        finalName = name;
                        break;
                }
            }

            merged.Components.Schemas![finalName] = CloneSchemaDefinition(schema, maps, name);
        }
    }

    private void MergeNamedComponents<T>(
        IDictionary<string, T>? source,
        IDictionary<string, T> target,
        IDictionary<string, string> owners,
        MergeComponentConflictPolicy policy,
        ReferenceType referenceType,
        string label,
        string sourceName,
        SourceComponentMaps maps,
        List<MergeDiagnostic> diagnostics,
        OpenApiDocument merged,
        Func<T, SourceComponentMaps, string, T> cloneDefinition,
        Func<T, T, bool> areEquivalent)
        where T : class
    {
        if (source is null) return;

        foreach (var (name, component) in source)
        {
            var conflict = _conflictDetector.Detect(label, name, sourceName, target, owners, areEquivalent, component);

            if (conflict.Kind == MergeConflictKind.Unique)
            {
                maps.Register(referenceType, name, name);
                owners[name] = sourceName;
                target[name] = cloneDefinition(component, maps, name);
                continue;
            }

            maps.Register(referenceType, name, name);

            if (conflict.Kind == MergeConflictKind.IdenticalDuplicate)
            {
                if (policy.Identical == MergeDuplicateHandling.Fail)
                {
                    diagnostics.Add(new MergeDiagnostic(
                        MergeDiagnosticSeverity.Error,
                        "Identical " + label + " '" + name + "' from '" + sourceName + "' duplicates the definition from '" + conflict.ExistingOwner + "'.",
                        sourceName));
                }
                else if (policy.Identical == MergeDuplicateHandling.WarnAndDedupe)
                {
                    diagnostics.Add(new MergeDiagnostic(
                        MergeDiagnosticSeverity.Warning,
                        "Identical " + label + " '" + name + "' from '" + sourceName + "' deduplicated against '" + conflict.ExistingOwner + "'.",
                        sourceName));
                }

                continue;
            }

            switch (policy.Conflict)
            {
                case MergeConflictResolution.Fail:
                    diagnostics.Add(new MergeDiagnostic(
                        MergeDiagnosticSeverity.Error,
                        "Conflicting " + label + " '" + name + "' from '" + sourceName + "' already exists from '" + conflict.ExistingOwner + "'.",
                        sourceName));
                    break;

                case MergeConflictResolution.KeepExisting:
                    diagnostics.Add(new MergeDiagnostic(
                        MergeDiagnosticSeverity.Warning,
                        "Conflicting " + label + " '" + name + "' from '" + sourceName + "'; keeping existing definition from '" + conflict.ExistingOwner + "'.",
                        sourceName));
                    break;

                case MergeConflictResolution.KeepIncoming:
                    diagnostics.Add(new MergeDiagnostic(
                        MergeDiagnosticSeverity.Warning,
                        "Conflicting " + label + " '" + name + "' from '" + sourceName + "' replaced the existing definition from '" + conflict.ExistingOwner + "'.",
                        sourceName));
                    owners[name] = sourceName;
                    target[name] = cloneDefinition(component, maps, name);
                    break;

                case MergeConflictResolution.RenameExisting:
                    var renamedExisting = RenameExistingComponent(merged, target, owners, referenceType, name, conflict.ExistingOwner ?? "existing");
                    diagnostics.Add(new MergeDiagnostic(
                        MergeDiagnosticSeverity.Warning,
                        "Existing " + label + " '" + name + "' from '" + conflict.ExistingOwner + "' renamed to '" + renamedExisting + "'; incoming definition from '" + sourceName + "' kept as '" + name + "'.",
                        sourceName));
                    owners[name] = sourceName;
                    target[name] = cloneDefinition(component, maps, name);
                    break;

                case MergeConflictResolution.RenameIncoming:
                    var finalName = BuildScopedName(sourceName, name, owners);
                    maps.Register(referenceType, name, finalName);
                    owners[finalName] = sourceName;
                    target[finalName] = cloneDefinition(component, maps, name);
                    diagnostics.Add(new MergeDiagnostic(
                        MergeDiagnosticSeverity.Warning,
                        "Conflicting " + label + " '" + name + "' from '" + sourceName + "' renamed to '" + finalName + "'.",
                        sourceName));
                    break;

                case MergeConflictResolution.RenameBoth:
                    var renamedExistingForBoth = RenameExistingComponent(merged, target, owners, referenceType, name, conflict.ExistingOwner ?? "existing");
                    var renamedIncomingForBoth = BuildScopedName(sourceName, name, owners);
                    maps.Register(referenceType, name, renamedIncomingForBoth);
                    owners[renamedIncomingForBoth] = sourceName;
                    target[renamedIncomingForBoth] = cloneDefinition(component, maps, name);
                    diagnostics.Add(new MergeDiagnostic(
                        MergeDiagnosticSeverity.Warning,
                        "Existing " + label + " '" + name + "' from '" + conflict.ExistingOwner + "' renamed to '" + renamedExistingForBoth + "', and incoming definition from '" + sourceName + "' renamed to '" + renamedIncomingForBoth + "'.",
                        sourceName));
                    break;
            }
        }
    }

    private static string BuildScopedName(string sourceName, string name, IDictionary<string, string> registry)
    {
        var scopedName = sourceName.Replace(" ", "_").Replace("-", "_") + "_" + name;
        return EnsureUniqueName(scopedName, registry);
    }

    private static string EnsureUniqueName(string name, IDictionary<string, string> registry)
    {
        if (!registry.ContainsKey(name)) return name;
        var i = 1;
        while (registry.ContainsKey(name + "_" + i)) i++;
        return name + "_" + i;
    }

    private static string RenameExistingComponent<T>(
        OpenApiDocument merged,
        IDictionary<string, T> target,
        IDictionary<string, string> owners,
        ReferenceType referenceType,
        string currentName,
        string ownerName)
        where T : class
    {
        var renamedName = BuildScopedName(ownerName, currentName, owners);
        target[renamedName] = target[currentName];
        target.Remove(currentName);

        owners.Remove(currentName);
        owners[renamedName] = ownerName;

        RewriteReferencesInDocument(merged, referenceType, currentName, renamedName);
        return renamedName;
    }

    private static void RewriteReferencesInDocument(OpenApiDocument document, ReferenceType referenceType, string currentName, string renamedName)
    {
        foreach (var pathItem in document.Paths.Values)
            RewritePathItemReferences(pathItem, referenceType, currentName, renamedName);

        var components = document.Components;
        if (components is null)
            return;

        RewriteSchemaCollection(components.Schemas, referenceType, currentName, renamedName);
        RewriteParameterCollection(components.Parameters, referenceType, currentName, renamedName);
        RewriteResponseCollection(components.Responses, referenceType, currentName, renamedName);
        RewriteRequestBodyCollection(components.RequestBodies, referenceType, currentName, renamedName);
        RewriteHeaderCollection(components.Headers, referenceType, currentName, renamedName);
        RewriteExampleCollection(components.Examples, referenceType, currentName, renamedName);
        RewriteLinkCollection(components.Links, referenceType, currentName, renamedName);
        RewriteCallbackCollection(components.Callbacks, referenceType, currentName, renamedName);
        RewriteSecurityRequirementList(document.SecurityRequirements, referenceType, currentName, renamedName);
    }

    private static void RewritePathItemReferences(OpenApiPathItem pathItem, ReferenceType referenceType, string currentName, string renamedName)
    {
        if (pathItem.Parameters is not null)
        {
            foreach (var parameter in pathItem.Parameters)
                RewriteParameterReferences(parameter, referenceType, currentName, renamedName);
        }

        foreach (var operation in pathItem.Operations.Values)
            RewriteOperationReferences(operation, referenceType, currentName, renamedName);
    }

    private static void RewriteOperationReferences(OpenApiOperation operation, ReferenceType referenceType, string currentName, string renamedName)
    {
        if (operation.Parameters is not null)
        {
            foreach (var parameter in operation.Parameters)
                RewriteParameterReferences(parameter, referenceType, currentName, renamedName);
        }

        if (operation.RequestBody is not null)
            RewriteRequestBodyReferences(operation.RequestBody, referenceType, currentName, renamedName);

        foreach (var response in operation.Responses.Values)
            RewriteResponseReferences(response, referenceType, currentName, renamedName);

        RewriteSecurityRequirementList(operation.Security, referenceType, currentName, renamedName);
    }

    private static void RewriteSchemaCollection(IDictionary<string, OpenApiSchema>? collection, ReferenceType referenceType, string currentName, string renamedName)
    {
        if (collection is null) return;
        foreach (var schema in collection.Values)
            RewriteSchemaReferences(schema, referenceType, currentName, renamedName);
    }

    private static void RewriteParameterCollection(IDictionary<string, OpenApiParameter>? collection, ReferenceType referenceType, string currentName, string renamedName)
    {
        if (collection is null) return;
        foreach (var parameter in collection.Values)
            RewriteParameterReferences(parameter, referenceType, currentName, renamedName);
    }

    private static void RewriteResponseCollection(IDictionary<string, OpenApiResponse>? collection, ReferenceType referenceType, string currentName, string renamedName)
    {
        if (collection is null) return;
        foreach (var response in collection.Values)
            RewriteResponseReferences(response, referenceType, currentName, renamedName);
    }

    private static void RewriteRequestBodyCollection(IDictionary<string, OpenApiRequestBody>? collection, ReferenceType referenceType, string currentName, string renamedName)
    {
        if (collection is null) return;
        foreach (var requestBody in collection.Values)
            RewriteRequestBodyReferences(requestBody, referenceType, currentName, renamedName);
    }

    private static void RewriteHeaderCollection(IDictionary<string, OpenApiHeader>? collection, ReferenceType referenceType, string currentName, string renamedName)
    {
        if (collection is null) return;
        foreach (var header in collection.Values)
            RewriteHeaderReferences(header, referenceType, currentName, renamedName);
    }

    private static void RewriteExampleCollection(IDictionary<string, OpenApiExample>? collection, ReferenceType referenceType, string currentName, string renamedName)
    {
        if (collection is null) return;
        foreach (var example in collection.Values)
            RewriteExampleReferences(example, referenceType, currentName, renamedName);
    }

    private static void RewriteLinkCollection(IDictionary<string, OpenApiLink>? collection, ReferenceType referenceType, string currentName, string renamedName)
    {
        if (collection is null) return;
        foreach (var link in collection.Values)
            RewriteLinkReferences(link, referenceType, currentName, renamedName);
    }

    private static void RewriteCallbackCollection(IDictionary<string, OpenApiCallback>? collection, ReferenceType referenceType, string currentName, string renamedName)
    {
        if (collection is null) return;
        foreach (var callback in collection.Values)
            RewriteCallbackReferences(callback, referenceType, currentName, renamedName);
    }

    private static void RewriteSchemaReferences(OpenApiSchema schema, ReferenceType referenceType, string currentName, string renamedName)
    {
        RewriteReference(schema.Reference, referenceType, currentName, renamedName);
        RewriteSchemaList(schema.AllOf, referenceType, currentName, renamedName);
        RewriteSchemaList(schema.OneOf, referenceType, currentName, renamedName);
        RewriteSchemaList(schema.AnyOf, referenceType, currentName, renamedName);
        if (schema.Not is not null) RewriteSchemaReferences(schema.Not, referenceType, currentName, renamedName);
        if (schema.Items is not null) RewriteSchemaReferences(schema.Items, referenceType, currentName, renamedName);
        if (schema.Properties is not null)
        {
            foreach (var property in schema.Properties.Values)
                RewriteSchemaReferences(property, referenceType, currentName, renamedName);
        }
        if (schema.AdditionalProperties is not null)
            RewriteSchemaReferences(schema.AdditionalProperties, referenceType, currentName, renamedName);
    }

    private static void RewriteSchemaList(IList<OpenApiSchema>? schemas, ReferenceType referenceType, string currentName, string renamedName)
    {
        if (schemas is null) return;
        foreach (var schema in schemas)
            RewriteSchemaReferences(schema, referenceType, currentName, renamedName);
    }

    private static void RewriteParameterReferences(OpenApiParameter parameter, ReferenceType referenceType, string currentName, string renamedName)
    {
        RewriteReference(parameter.Reference, referenceType, currentName, renamedName);
        if (parameter.Schema is not null) RewriteSchemaReferences(parameter.Schema, referenceType, currentName, renamedName);
        if (parameter.Examples is not null)
        {
            foreach (var example in parameter.Examples.Values)
                RewriteExampleReferences(example, referenceType, currentName, renamedName);
        }
        if (parameter.Content is not null)
        {
            foreach (var mediaType in parameter.Content.Values)
                RewriteMediaTypeReferences(mediaType, referenceType, currentName, renamedName);
        }
    }

    private static void RewriteRequestBodyReferences(OpenApiRequestBody requestBody, ReferenceType referenceType, string currentName, string renamedName)
    {
        RewriteReference(requestBody.Reference, referenceType, currentName, renamedName);
        if (requestBody.Content is null) return;
        foreach (var mediaType in requestBody.Content.Values)
            RewriteMediaTypeReferences(mediaType, referenceType, currentName, renamedName);
    }

    private static void RewriteResponseReferences(OpenApiResponse response, ReferenceType referenceType, string currentName, string renamedName)
    {
        RewriteReference(response.Reference, referenceType, currentName, renamedName);
        if (response.Headers is not null)
        {
            foreach (var header in response.Headers.Values)
                RewriteHeaderReferences(header, referenceType, currentName, renamedName);
        }
        if (response.Content is not null)
        {
            foreach (var mediaType in response.Content.Values)
                RewriteMediaTypeReferences(mediaType, referenceType, currentName, renamedName);
        }
        if (response.Links is not null)
        {
            foreach (var link in response.Links.Values)
                RewriteLinkReferences(link, referenceType, currentName, renamedName);
        }
    }

    private static void RewriteHeaderReferences(OpenApiHeader header, ReferenceType referenceType, string currentName, string renamedName)
    {
        RewriteReference(header.Reference, referenceType, currentName, renamedName);
        if (header.Schema is not null) RewriteSchemaReferences(header.Schema, referenceType, currentName, renamedName);
        if (header.Examples is not null)
        {
            foreach (var example in header.Examples.Values)
                RewriteExampleReferences(example, referenceType, currentName, renamedName);
        }
        if (header.Content is not null)
        {
            foreach (var mediaType in header.Content.Values)
                RewriteMediaTypeReferences(mediaType, referenceType, currentName, renamedName);
        }
    }

    private static void RewriteExampleReferences(OpenApiExample example, ReferenceType referenceType, string currentName, string renamedName)
    {
        RewriteReference(example.Reference, referenceType, currentName, renamedName);
    }

    private static void RewriteLinkReferences(OpenApiLink link, ReferenceType referenceType, string currentName, string renamedName)
    {
        RewriteReference(link.Reference, referenceType, currentName, renamedName);
    }

    private static void RewriteCallbackReferences(OpenApiCallback callback, ReferenceType referenceType, string currentName, string renamedName)
    {
        RewriteReference(callback.Reference, referenceType, currentName, renamedName);
        foreach (var pathItem in callback.PathItems.Values)
            RewritePathItemReferences(pathItem, referenceType, currentName, renamedName);
    }

    private static void RewriteMediaTypeReferences(OpenApiMediaType mediaType, ReferenceType referenceType, string currentName, string renamedName)
    {
        if (mediaType.Schema is not null) RewriteSchemaReferences(mediaType.Schema, referenceType, currentName, renamedName);
        if (mediaType.Examples is not null)
        {
            foreach (var example in mediaType.Examples.Values)
                RewriteExampleReferences(example, referenceType, currentName, renamedName);
        }
        if (mediaType.Encoding is not null)
        {
            foreach (var encoding in mediaType.Encoding.Values)
            {
                if (encoding.Headers is null) continue;
                foreach (var header in encoding.Headers.Values)
                    RewriteHeaderReferences(header, referenceType, currentName, renamedName);
            }
        }
    }

    private static void RewriteSecurityRequirementList(IList<OpenApiSecurityRequirement>? requirements, ReferenceType referenceType, string currentName, string renamedName)
    {
        if (requirements is null) return;
        foreach (var requirement in requirements)
        {
            var renamedEntries = requirement
                .Where(pair => pair.Key.Reference?.Type == referenceType && string.Equals(pair.Key.Reference.Id, currentName, StringComparison.OrdinalIgnoreCase))
                .Select(pair => (Scheme: pair.Key, Scopes: pair.Value))
                .ToList();

            foreach (var (scheme, scopes) in renamedEntries)
            {
                requirement.Remove(scheme);
                var replacement = new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Id = renamedName,
                        Type = referenceType,
                        ExternalResource = scheme.Reference?.ExternalResource,
                    },
                    UnresolvedReference = true,
                };
                requirement[replacement] = scopes;
            }
        }
    }

    private static void RewriteReference(OpenApiReference? reference, ReferenceType referenceType, string currentName, string renamedName)
    {
        if (reference?.Type == referenceType && string.Equals(reference.Id, currentName, StringComparison.OrdinalIgnoreCase))
            reference.Id = renamedName;
    }

    private void MergeSecuritySchemes(
        OpenApiDocument doc,
        string sourceName,
        SourceComponentMaps maps,
        OpenApiDocument merged,
        List<MergeDiagnostic> diagnostics,
        IDictionary<string, string> owners,
        MergeComponentConflictPolicy policy)
    {
        if (doc.Components?.SecuritySchemes is null) return;

        foreach (var (name, scheme) in doc.Components.SecuritySchemes)
        {
            var conflict = _conflictDetector.Detect(
                "security scheme",
                name,
                sourceName,
                merged.Components.SecuritySchemes!,
                owners,
                _componentComparer.AreEquivalent,
                scheme);

            if (conflict.Kind == MergeConflictKind.Unique)
            {
                maps.Register(ReferenceType.SecurityScheme, name, name);
                owners[name] = sourceName;
                merged.Components.SecuritySchemes![name] = CloneSecuritySchemeDefinition(scheme, maps, name);
                continue;
            }

            maps.Register(ReferenceType.SecurityScheme, name, name);

            if (conflict.Kind == MergeConflictKind.IdenticalDuplicate)
            {
                if (policy.Identical == MergeDuplicateHandling.Fail)
                {
                    diagnostics.Add(new MergeDiagnostic(
                        MergeDiagnosticSeverity.Error,
                        "Identical security scheme '" + name + "' from '" + sourceName + "' duplicates the definition from '" + conflict.ExistingOwner + "'.",
                        sourceName));
                }
                else if (policy.Identical == MergeDuplicateHandling.WarnAndDedupe)
                {
                    diagnostics.Add(new MergeDiagnostic(
                        MergeDiagnosticSeverity.Warning,
                        "Identical security scheme '" + name + "' from '" + sourceName + "' deduplicated against '" + conflict.ExistingOwner + "'.",
                        sourceName));
                }

                continue;
            }

            switch (policy.Conflict)
            {
                case MergeConflictResolution.Fail:
                    diagnostics.Add(new MergeDiagnostic(
                        MergeDiagnosticSeverity.Error,
                        "Conflicting security scheme '" + name + "' from '" + sourceName + "' already exists from '" + conflict.ExistingOwner + "'.",
                        sourceName));
                    break;

                case MergeConflictResolution.KeepExisting:
                    diagnostics.Add(new MergeDiagnostic(
                        MergeDiagnosticSeverity.Warning,
                        "Conflicting security scheme '" + name + "' from '" + sourceName + "'; keeping existing definition from '" + conflict.ExistingOwner + "'.",
                        sourceName));
                    break;

                case MergeConflictResolution.KeepIncoming:
                    diagnostics.Add(new MergeDiagnostic(
                        MergeDiagnosticSeverity.Warning,
                        "Conflicting security scheme '" + name + "' from '" + sourceName + "' replaced the existing definition from '" + conflict.ExistingOwner + "'.",
                        sourceName));
                    owners[name] = sourceName;
                    merged.Components.SecuritySchemes![name] = CloneSecuritySchemeDefinition(scheme, maps, name);
                    break;

                case MergeConflictResolution.RenameExisting:
                    var renamedExistingScheme = RenameExistingComponent(
                        merged,
                        merged.Components.SecuritySchemes!,
                        owners,
                        ReferenceType.SecurityScheme,
                        name,
                        conflict.ExistingOwner ?? "existing");
                    diagnostics.Add(new MergeDiagnostic(
                        MergeDiagnosticSeverity.Warning,
                        "Existing security scheme '" + name + "' from '" + conflict.ExistingOwner + "' renamed to '" + renamedExistingScheme + "'; incoming definition from '" + sourceName + "' kept as '" + name + "'.",
                        sourceName));
                    owners[name] = sourceName;
                    merged.Components.SecuritySchemes![name] = CloneSecuritySchemeDefinition(scheme, maps, name);
                    break;

                case MergeConflictResolution.RenameIncoming:
                    var finalName = BuildScopedName(sourceName, name, owners);
                    maps.Register(ReferenceType.SecurityScheme, name, finalName);
                    owners[finalName] = sourceName;
                    merged.Components.SecuritySchemes![finalName] = CloneSecuritySchemeDefinition(scheme, maps, name);
                    diagnostics.Add(new MergeDiagnostic(
                        MergeDiagnosticSeverity.Warning,
                        "Conflicting security scheme '" + name + "' from '" + sourceName + "' renamed to '" + finalName + "'.",
                        sourceName));
                    break;

                case MergeConflictResolution.RenameBoth:
                    var renamedExistingSchemeForBoth = RenameExistingComponent(
                        merged,
                        merged.Components.SecuritySchemes!,
                        owners,
                        ReferenceType.SecurityScheme,
                        name,
                        conflict.ExistingOwner ?? "existing");
                    var renamedIncomingSchemeForBoth = BuildScopedName(sourceName, name, owners);
                    maps.Register(ReferenceType.SecurityScheme, name, renamedIncomingSchemeForBoth);
                    owners[renamedIncomingSchemeForBoth] = sourceName;
                    merged.Components.SecuritySchemes![renamedIncomingSchemeForBoth] = CloneSecuritySchemeDefinition(scheme, maps, name);
                    diagnostics.Add(new MergeDiagnostic(
                        MergeDiagnosticSeverity.Warning,
                        "Existing security scheme '" + name + "' from '" + conflict.ExistingOwner + "' renamed to '" + renamedExistingSchemeForBoth + "', and incoming definition from '" + sourceName + "' renamed to '" + renamedIncomingSchemeForBoth + "'.",
                        sourceName));
                    break;
            }
        }
    }

    private static void MergePaths(
        OpenApiDocument doc,
        string sourceName,
        string pathPrefix,
        string opIdPrefix,
        HashSet<string> seenOperationIds,
        OpenApiDocument merged,
        List<MergeDiagnostic> diagnostics,
        SourceComponentMaps maps)
    {
        if (doc.Paths is null) return;

        foreach (var (path, item) in doc.Paths)
        {
            var newPath = pathPrefix + path;

            if (!merged.Paths.TryGetValue(newPath, out var newItem))
            {
                newItem = new OpenApiPathItem
                {
                    Summary = item.Summary,
                    Description = item.Description,
                    Servers = item.Servers?.Select(CloneServer).ToList(),
                    Parameters = item.Parameters?.Select(p => CloneParameterReferenceOrInline(p, maps)).ToList(),
                };
                merged.Paths[newPath] = newItem;
            }

            foreach (var (method, op) in item.Operations)
            {
                if (newItem.Operations.ContainsKey(method))
                {
                    diagnostics.Add(new MergeDiagnostic(
                        MergeDiagnosticSeverity.Warning,
                        "Operation '" + method + " " + newPath + "' from '" + sourceName + "' already exists; skipping.",
                        sourceName));
                    continue;
                }

                var newOp = CloneOperation(op, opIdPrefix, maps);

                if (!string.IsNullOrEmpty(newOp.OperationId) && !seenOperationIds.Add(newOp.OperationId))
                {
                    diagnostics.Add(new MergeDiagnostic(
                        MergeDiagnosticSeverity.Warning,
                        "Duplicate operationId '" + newOp.OperationId + "' from '" + sourceName + "'.",
                        sourceName));
                }

                newItem.Operations[method] = newOp;
            }
        }
    }

    private static void MergeTags(OpenApiDocument doc, OpenApiDocument merged)
    {
        if (doc.Tags is null) return;
        merged.Tags ??= [];
        var existing = merged.Tags
            .Where(t => !string.IsNullOrEmpty(t.Name))
            .ToDictionary(t => t.Name!, StringComparer.OrdinalIgnoreCase);

        foreach (var tag in doc.Tags)
        {
            var tagName = tag.Name ?? "";
            if (string.IsNullOrEmpty(tagName))
                continue;

            if (!existing.TryGetValue(tagName, out var existingTag))
            {
                var clonedTag = CloneTag(tag);
                merged.Tags.Add(clonedTag);
                existing[tagName] = clonedTag;
                continue;
            }

            existingTag.Description ??= tag.Description;
            if (existingTag.ExternalDocs is null && tag.ExternalDocs is not null)
                existingTag.ExternalDocs = CloneExternalDocs(tag.ExternalDocs);
            else if (existingTag.ExternalDocs is not null && tag.ExternalDocs is not null)
                existingTag.ExternalDocs.Description ??= tag.ExternalDocs.Description;
        }
    }

    private static void MergeDocumentSecurity(OpenApiDocument doc, OpenApiDocument merged, SourceComponentMaps maps)
    {
        if (doc.SecurityRequirements is null) return;
        merged.SecurityRequirements ??= [];
        var existingFingerprints = merged.SecurityRequirements
            .Select(GetSecurityRequirementFingerprint)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var requirement in doc.SecurityRequirements)
        {
            var cloned = CloneSecurityRequirement(requirement, maps);
            var fingerprint = GetSecurityRequirementFingerprint(cloned);
            if (existingFingerprints.Add(fingerprint))
                merged.SecurityRequirements.Add(cloned);
        }
    }

    private static OpenApiTag CloneTag(OpenApiTag tag)
    {
        return new OpenApiTag
        {
            Name = tag.Name,
            Description = tag.Description,
            ExternalDocs = tag.ExternalDocs is null ? null : CloneExternalDocs(tag.ExternalDocs),
        };
    }

    private static OpenApiExternalDocs CloneExternalDocs(OpenApiExternalDocs docs)
    {
        return new OpenApiExternalDocs
        {
            Url = docs.Url,
            Description = docs.Description,
        };
    }

    private static string GetSecurityRequirementFingerprint(OpenApiSecurityRequirement requirement)
    {
        return string.Join("|", requirement
            .Select(pair => new
            {
                Scheme = pair.Key.Reference?.Id ?? pair.Key.Name ?? pair.Key.Scheme ?? string.Empty,
                Scopes = pair.Value?.OrderBy(scope => scope, StringComparer.Ordinal).ToArray() ?? [],
            })
            .OrderBy(pair => pair.Scheme, StringComparer.Ordinal)
            .Select(pair => pair.Scheme + ":" + string.Join(",", pair.Scopes)));
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
