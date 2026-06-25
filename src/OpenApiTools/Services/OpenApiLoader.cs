using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;

namespace OpenApiTools.Services;

public sealed class OpenApiLoader : IOpenApiLoader
{
    public async Task<OpenApiDocument> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"OpenAPI document not found: {path}", path);
        }

        await using var stream = File.OpenRead(path);
        var reader = new OpenApiStreamReader();
        var result = await reader.ReadAsync(stream, cancellationToken);

        if (result.OpenApiDocument is null)
        {
            var errors = string.Join("; ", result.OpenApiDiagnostic?.Errors ?? []);
            throw new InvalidOperationException(
                $"Failed to read OpenAPI document from '{path}'. Errors: {errors}");
        }

        return result.OpenApiDocument;
    }
}