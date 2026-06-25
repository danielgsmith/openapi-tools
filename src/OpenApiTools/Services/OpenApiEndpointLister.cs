using Microsoft.OpenApi.Models;

namespace OpenApiTools.Services;

public interface IOpenApiEndpointLister
{
    IReadOnlyList<EndpointInfo> List(OpenApiDocument document);
}

public sealed record EndpointInfo(
    string Method,
    string Path,
    string? OperationId,
    string? Summary,
    string? Description,
    IReadOnlyList<string> Tags,
    bool Deprecated);

public sealed class OpenApiEndpointLister : IOpenApiEndpointLister
{
    public IReadOnlyList<EndpointInfo> List(OpenApiDocument document)
    {
        var results = new List<EndpointInfo>();

        foreach (var (path, item) in document.Paths ?? new OpenApiPaths())
        {
            foreach (var (method, op) in item.Operations)
            {
                results.Add(new EndpointInfo(
                    Method: method.ToString().ToUpperInvariant(),
                    Path: path,
                    OperationId: op.OperationId,
                    Summary: op.Summary,
                    Description: op.Description,
                    Tags: (op.Tags ?? []).Select(t => t.Name ?? "").Where(n => !string.IsNullOrEmpty(n)).ToArray(),
                    Deprecated: op.Deprecated));
            }
        }

        return results;
    }
}