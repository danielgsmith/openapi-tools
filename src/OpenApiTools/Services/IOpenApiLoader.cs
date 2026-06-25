using Microsoft.OpenApi.Models;

namespace OpenApiTools.Services;

public interface IOpenApiLoader
{
    Task<OpenApiDocument> LoadAsync(string path, CancellationToken cancellationToken = default);
}
