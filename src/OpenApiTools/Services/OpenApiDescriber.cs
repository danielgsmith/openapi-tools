using Microsoft.OpenApi.Models;

namespace OpenApiTools.Services;

public interface IOpenApiDescriber
{
    EndpointDetail DescribeEndpoint(OpenApiDocument document, string endpointPath);
    SchemaDetail DescribeSchema(OpenApiDocument document, string schemaName);
}

public sealed record EndpointDetail(
    string Path,
    string Method,
    string? OperationId,
    string? Summary,
    string? Description,
    bool Deprecated,
    IReadOnlyList<ParamDetail> Parameters,
    RequestBodyDetail? RequestBody,
    IReadOnlyList<ResponseDetail> Responses,
    IReadOnlyList<string> Tags);

public sealed record ParamDetail(
    string Name,
    string In,
    bool Required,
    string? Type,
    string? Description);

public sealed record RequestBodyDetail(
    string? Description,
    bool Required,
    IReadOnlyList<string> ContentTypes);

public sealed record ResponseDetail(
    string StatusCode,
    string? Description,
    IReadOnlyList<string> ContentTypes);

public sealed record SchemaDetail(
    string Name,
    string? Type,
    string? Format,
    string? Description,
    bool Deprecated,
    IReadOnlyList<string> Required,
    IReadOnlyList<PropertyDetail> Properties);

public sealed record PropertyDetail(
    string Name,
    string? Type,
    string? Format,
    string? Description,
    bool Required);

public sealed class OpenApiDescriber : IOpenApiDescriber
{
    public EndpointDetail DescribeEndpoint(OpenApiDocument document, string endpointPath)
    {
        var (path, method) = ParseEndpointPath(endpointPath);

        if (!document.Paths.TryGetValue(path, out var pathItem))
            throw new KeyNotFoundException($"Path '{path}' not found.");

        if (!pathItem.Operations.TryGetValue(method, out var op))
            throw new KeyNotFoundException($"Operation '{method}' not found on path '{path}'.");

        var parameters = (op.Parameters ?? []).Select(p => new ParamDetail(
            Name: p.Name ?? "",
            In: p.In?.ToString() ?? "",
            Required: p.Required,
            Type: p.Schema?.Type,
            Description: p.Description)).ToArray();

        var requestBody = op.RequestBody is null ? null : new RequestBodyDetail(
            Description: op.RequestBody.Description,
            Required: op.RequestBody.Required,
            ContentTypes: (op.RequestBody.Content ?? new Dictionary<string, OpenApiMediaType>()).Keys.ToArray());

        var responses = (op.Responses ?? new Dictionary<string, OpenApiResponse>()).OrderBy(r => r.Key).Select(r => new ResponseDetail(
            StatusCode: r.Key,
            Description: r.Value.Description,
            ContentTypes: (r.Value.Content ?? new Dictionary<string, OpenApiMediaType>()).Keys.ToArray())).ToArray();

        return new EndpointDetail(
            Path: path,
            Method: method.ToString().ToUpperInvariant(),
            OperationId: op.OperationId,
            Summary: op.Summary,
            Description: op.Description,
            Deprecated: op.Deprecated,
            Parameters: parameters,
            RequestBody: requestBody,
            Responses: responses,
            Tags: (op.Tags ?? []).Select(t => t.Name ?? "").Where(n => !string.IsNullOrEmpty(n)).ToArray());
    }

    public SchemaDetail DescribeSchema(OpenApiDocument document, string schemaName)
    {
        if (document.Components?.Schemas is null || !document.Components.Schemas.TryGetValue(schemaName, out var schema))
            throw new KeyNotFoundException($"Schema '{schemaName}' not found.");

        var properties = (schema.Properties ?? new Dictionary<string, OpenApiSchema>()).Select(p => new PropertyDetail(
            Name: p.Key,
            Type: p.Value.Type,
            Format: p.Value.Format,
            Description: p.Value.Description,
            Required: schema.Required?.Contains(p.Key) ?? false)).ToArray();

        return new SchemaDetail(
            Name: schemaName,
            Type: schema.Type,
            Format: schema.Format,
            Description: schema.Description,
            Deprecated: schema.Deprecated,
            Required: (schema.Required ?? new HashSet<string>()).ToArray(),
            Properties: properties);
    }

    private static (string Path, OperationType Method) ParseEndpointPath(string endpointPath)
    {
        var parts = endpointPath.Split(' ', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
            throw new ArgumentException($"Endpoint must be in format 'METHOD /path', got: '{endpointPath}'");

        if (!Enum.TryParse<OperationType>(parts[0], ignoreCase: true, out var method))
            throw new ArgumentException($"Unknown HTTP method: '{parts[0]}'");

        return (parts[1], method);
    }
}