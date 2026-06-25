using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Writers;

namespace OpenApiTools.Services;

public interface IOpenApiResolver
{
    string ResolveAndSerialize(OpenApiDocument document, string format);
}

public sealed class OpenApiResolver : IOpenApiResolver
{
    public string ResolveAndSerialize(OpenApiDocument document, string format)
    {
        using var stringWriter = new StringWriter();
        var settings = new OpenApiJsonWriterSettings
        {
            InlineLocalReferences = true,
        };

        IOpenApiWriter writer = format.ToLowerInvariant() switch
        {
            "yaml" or "yml" => new OpenApiYamlWriter(stringWriter, settings),
            _ => new OpenApiJsonWriter(stringWriter, settings),
        };

        document.SerializeAsV3(writer);
        return stringWriter.ToString();
    }
}