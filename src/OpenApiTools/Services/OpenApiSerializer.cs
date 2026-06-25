using Microsoft.OpenApi.Models;

namespace OpenApiTools.Services;

public interface IOpenApiSerializer
{
    string SerializeAsJson(OpenApiDocument document);
    string SerializeAsYaml(OpenApiDocument document);
    Task SerializeToFileAsync(OpenApiDocument document, string path, string format);
}

public sealed class OpenApiSerializer : IOpenApiSerializer
{
    public string SerializeAsJson(OpenApiDocument document)
    {
        using var stringWriter = new StringWriter();
        var writer = new Microsoft.OpenApi.Writers.OpenApiJsonWriter(stringWriter);
        document.SerializeAsV3(writer);
        return stringWriter.ToString();
    }

    public string SerializeAsYaml(OpenApiDocument document)
    {
        using var stringWriter = new StringWriter();
        var writer = new Microsoft.OpenApi.Writers.OpenApiYamlWriter(stringWriter);
        document.SerializeAsV3(writer);
        return stringWriter.ToString();
    }

    public async Task SerializeToFileAsync(OpenApiDocument document, string path, string format)
    {
        var content = format.ToLowerInvariant() switch
        {
            "yaml" or "yml" => SerializeAsYaml(document),
            _ => SerializeAsJson(document),
        };
        await File.WriteAllTextAsync(path, content);
    }
}