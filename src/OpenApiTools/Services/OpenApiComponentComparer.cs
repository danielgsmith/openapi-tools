using System.Text;
using System.Text.Json;
using Microsoft.OpenApi.Interfaces;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Writers;

namespace OpenApiTools.Services;

public interface IOpenApiComponentComparer
{
    bool AreEquivalent(OpenApiSchema left, OpenApiSchema right);
    bool AreEquivalent(OpenApiParameter left, OpenApiParameter right);
    bool AreEquivalent(OpenApiResponse left, OpenApiResponse right);
    bool AreEquivalent(OpenApiRequestBody left, OpenApiRequestBody right);
    bool AreEquivalent(OpenApiHeader left, OpenApiHeader right);
    bool AreEquivalent(OpenApiExample left, OpenApiExample right);
    bool AreEquivalent(OpenApiLink left, OpenApiLink right);
    bool AreEquivalent(OpenApiCallback left, OpenApiCallback right);
    bool AreEquivalent(OpenApiSecurityScheme left, OpenApiSecurityScheme right);
}

public sealed class OpenApiComponentComparer : IOpenApiComponentComparer
{
    public bool AreEquivalent(OpenApiSchema left, OpenApiSchema right) => Canonicalize(left.SerializeAsV3) == Canonicalize(right.SerializeAsV3);

    public bool AreEquivalent(OpenApiParameter left, OpenApiParameter right) => Canonicalize(left.SerializeAsV3) == Canonicalize(right.SerializeAsV3);

    public bool AreEquivalent(OpenApiResponse left, OpenApiResponse right) => Canonicalize(left.SerializeAsV3) == Canonicalize(right.SerializeAsV3);

    public bool AreEquivalent(OpenApiRequestBody left, OpenApiRequestBody right) => Canonicalize(left.SerializeAsV3) == Canonicalize(right.SerializeAsV3);

    public bool AreEquivalent(OpenApiHeader left, OpenApiHeader right) => Canonicalize(left.SerializeAsV3) == Canonicalize(right.SerializeAsV3);

    public bool AreEquivalent(OpenApiExample left, OpenApiExample right) => Canonicalize(left.SerializeAsV3) == Canonicalize(right.SerializeAsV3);

    public bool AreEquivalent(OpenApiLink left, OpenApiLink right) => Canonicalize(left.SerializeAsV3) == Canonicalize(right.SerializeAsV3);

    public bool AreEquivalent(OpenApiCallback left, OpenApiCallback right) => Canonicalize(left.SerializeAsV3) == Canonicalize(right.SerializeAsV3);

    public bool AreEquivalent(OpenApiSecurityScheme left, OpenApiSecurityScheme right) => Canonicalize(left.SerializeAsV3) == Canonicalize(right.SerializeAsV3);

    private static string Canonicalize(Action<IOpenApiWriter> write)
    {
        using var stringWriter = new StringWriter();
        var writer = new OpenApiJsonWriter(stringWriter);
        write(writer);

        using var json = JsonDocument.Parse(stringWriter.ToString());
        var builder = new StringBuilder();
        WriteCanonicalJson(json.RootElement, builder);
        return builder.ToString();
    }

    private static void WriteCanonicalJson(JsonElement element, StringBuilder builder)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                builder.Append('{');
                var firstProperty = true;
                foreach (var property in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    if (!firstProperty)
                        builder.Append(',');

                    firstProperty = false;
                    builder.Append(JsonSerializer.Serialize(property.Name));
                    builder.Append(':');
                    WriteCanonicalJson(property.Value, builder);
                }
                builder.Append('}');
                break;

            case JsonValueKind.Array:
                builder.Append('[');
                var firstItem = true;
                foreach (var item in element.EnumerateArray())
                {
                    if (!firstItem)
                        builder.Append(',');

                    firstItem = false;
                    WriteCanonicalJson(item, builder);
                }
                builder.Append(']');
                break;

            default:
                builder.Append(element.GetRawText());
                break;
        }
    }
}
