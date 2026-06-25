using Microsoft.OpenApi.Interfaces;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Writers;

namespace OpenApiTools.Services;

public interface IOpenApiSplitter
{
    Task SplitAsync(OpenApiDocument document, string outputDir, CancellationToken cancellationToken = default);
}

public sealed class OpenApiSplitter : IOpenApiSplitter
{
    public async Task SplitAsync(OpenApiDocument document, string outputDir, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDir);

        var mainDoc = CloneForMainFile(document);
        var mainPath = Path.Combine(outputDir, "openapi.yaml");
        await WriteYamlAsync(mainDoc, mainPath, cancellationToken);

        var components = document.Components;
        if (components is null) return;

        await WriteComponentsAsync(components.Schemas, "schemas", outputDir, cancellationToken);
        await WriteComponentsAsync(components.Parameters, "parameters", outputDir, cancellationToken);
        await WriteComponentsAsync(components.Responses, "responses", outputDir, cancellationToken);
        await WriteComponentsAsync(components.RequestBodies, "requestBodies", outputDir, cancellationToken);
        await WriteComponentsAsync(components.Headers, "headers", outputDir, cancellationToken);
        await WriteComponentsAsync(components.Examples, "examples", outputDir, cancellationToken);
        await WriteComponentsAsync(components.Links, "links", outputDir, cancellationToken);
        await WriteComponentsAsync(components.Callbacks, "callbacks", outputDir, cancellationToken);
        await WriteComponentsAsync(components.SecuritySchemes, "securitySchemes", outputDir, cancellationToken);
    }

    private static OpenApiDocument CloneForMainFile(OpenApiDocument document)
    {
        var main = new OpenApiDocument
        {
            Info = document.Info,
            Servers = document.Servers,
            Paths = document.Paths,
            SecurityRequirements = document.SecurityRequirements,
            Tags = document.Tags,
            ExternalDocs = document.ExternalDocs,
        };

        if (document.Components is not null)
        {
            main.Components = new OpenApiComponents
            {
                Schemas = ReplaceWithRefs(document.Components.Schemas, "schemas"),
                Parameters = ReplaceWithRefs(document.Components.Parameters, "parameters"),
                Responses = ReplaceWithRefs(document.Components.Responses, "responses"),
                RequestBodies = ReplaceWithRefs(document.Components.RequestBodies, "requestBodies"),
                Headers = ReplaceWithRefs(document.Components.Headers, "headers"),
                Examples = ReplaceWithRefs(document.Components.Examples, "examples"),
                Links = ReplaceWithRefs(document.Components.Links, "links"),
                Callbacks = ReplaceWithRefs(document.Components.Callbacks, "callbacks"),
                SecuritySchemes = ReplaceWithRefs(document.Components.SecuritySchemes, "securitySchemes"),
            };
        }

        return main;
    }

    private static Dictionary<string, T> ReplaceWithRefs<T>(IDictionary<string, T>? components, string folder)
        where T : class, IOpenApiReferenceable
    {
        if (components is null || components.Count == 0)
            return [];

        var result = new Dictionary<string, T>();
        foreach (var (name, _) in components)
        {
            var refPath = $"./components/{folder}/{name}.yaml";
            var clone = CreateReferencedStub<T>(refPath, name);
            if (clone is not null)
                result[name] = clone;
        }
        return result;
    }

    private static T? CreateReferencedStub<T>(string refPath, string name)
        where T : class
    {
        var refObj = new OpenApiReference
        {
            Id = name,
            Type = typeof(T).Name switch
            {
                nameof(OpenApiSchema) => ReferenceType.Schema,
                nameof(OpenApiParameter) => ReferenceType.Parameter,
                nameof(OpenApiResponse) => ReferenceType.Response,
                nameof(OpenApiRequestBody) => ReferenceType.RequestBody,
                nameof(OpenApiHeader) => ReferenceType.Header,
                nameof(OpenApiExample) => ReferenceType.Example,
                nameof(OpenApiLink) => ReferenceType.Link,
                nameof(OpenApiCallback) => ReferenceType.Callback,
                nameof(OpenApiSecurityScheme) => ReferenceType.SecurityScheme,
                _ => null,
            },
            ExternalResource = refPath,
        };

        return CreateInstance<T>(refObj);
    }

    private static T? CreateInstance<T>(OpenApiReference reference)
        where T : class
    {
        var type = typeof(T);
        var instance = Activator.CreateInstance(type);
        if (instance is null) return null;

        var refProp = type.GetProperty("Reference");
        var unresolvedProp = type.GetProperty("UnresolvedReference");
        refProp?.SetValue(instance, reference);
        unresolvedProp?.SetValue(instance, true);

        return (T)instance;
    }

    private static async Task WriteComponentsAsync<T>(
        IDictionary<string, T>? components,
        string folder,
        string outputDir,
        CancellationToken cancellationToken)
        where T : IOpenApiReferenceable
    {
        if (components is null || components.Count == 0) return;

        var dir = Path.Combine(outputDir, "components", folder);
        Directory.CreateDirectory(dir);

        foreach (var (name, component) in components)
        {
            var filePath = Path.Combine(dir, $"{name}.yaml");
            using var stringWriter = new StringWriter();
            var writer = new OpenApiYamlWriter(stringWriter);
            WriteComponent(writer, component);
            await File.WriteAllTextAsync(filePath, stringWriter.ToString(), cancellationToken);
        }
    }

    private static void WriteComponent(IOpenApiWriter writer, object component)
    {
        switch (component)
        {
            case OpenApiSchema s:
                writer.WriteStartObject();
                s.SerializeAsV3(writer);
                writer.WriteEndObject();
                break;
            case OpenApiParameter p:
                writer.WriteStartObject();
                p.SerializeAsV3(writer);
                writer.WriteEndObject();
                break;
            case OpenApiResponse r:
                writer.WriteStartObject();
                r.SerializeAsV3(writer);
                writer.WriteEndObject();
                break;
            case OpenApiRequestBody b:
                writer.WriteStartObject();
                b.SerializeAsV3(writer);
                writer.WriteEndObject();
                break;
            case OpenApiSecurityScheme ss:
                writer.WriteStartObject();
                ss.SerializeAsV3(writer);
                writer.WriteEndObject();
                break;
            case OpenApiExample e:
                writer.WriteStartObject();
                e.SerializeAsV3(writer);
                writer.WriteEndObject();
                break;
            case OpenApiHeader h:
                writer.WriteStartObject();
                h.SerializeAsV3(writer);
                writer.WriteEndObject();
                break;
            case OpenApiLink l:
                writer.WriteStartObject();
                l.SerializeAsV3(writer);
                writer.WriteEndObject();
                break;
            case OpenApiCallback c:
                writer.WriteStartObject();
                c.SerializeAsV3(writer);
                writer.WriteEndObject();
                break;
        }
    }

    private static async Task WriteYamlAsync(OpenApiDocument doc, string path, CancellationToken cancellationToken)
    {
        using var stringWriter = new StringWriter();
        var writer = new OpenApiYamlWriter(stringWriter);
        doc.SerializeAsV3(writer);
        await File.WriteAllTextAsync(path, stringWriter.ToString(), cancellationToken);
    }
}