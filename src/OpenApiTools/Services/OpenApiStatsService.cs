using Microsoft.OpenApi.Models;
using OpenApiTools.Models;

namespace OpenApiTools.Services;

public interface IOpenApiStatsService
{
    StatsResult Compute(OpenApiDocument document);
}

public sealed class OpenApiStatsService : IOpenApiStatsService
{
    public StatsResult Compute(OpenApiDocument document)
    {
        var pathCount = document.Paths?.Count ?? 0;
        var operationCount = 0;
        var deprecatedOps = 0;
        var opsWithoutDesc = 0;
        var methodsBreakdown = new Dictionary<string, int>();

        foreach (var (_, item) in document.Paths ?? new OpenApiPaths())
        {
            foreach (var (method, op) in item.Operations)
            {
                operationCount++;
                var methodStr = method.ToString().ToUpperInvariant();
                methodsBreakdown[methodStr] = methodsBreakdown.GetValueOrDefault(methodStr) + 1;

                if (op.Deprecated) deprecatedOps++;
                if (string.IsNullOrWhiteSpace(op.Description) && string.IsNullOrWhiteSpace(op.Summary))
                    opsWithoutDesc++;
            }
        }

        var schemas = document.Components?.Schemas;
        var schemaCount = schemas?.Count ?? 0;
        var schemasWithoutDesc = 0;
        var largestSchemas = new List<string>();

        if (schemas is not null)
        {
            foreach (var (name, schema) in schemas)
            {
                if (string.IsNullOrWhiteSpace(schema.Description))
                    schemasWithoutDesc++;
            }

            largestSchemas = schemas
                .OrderByDescending(s => CountSchemaProperties(s.Value))
                .Take(5)
                .Select(s => $"{s.Key} ({CountSchemaProperties(s.Value)} props)")
                .ToList();
        }

        return new StatsResult(
            PathCount: pathCount,
            OperationCount: operationCount,
            SchemaCount: schemaCount,
            ParameterCount: document.Components?.Parameters?.Count ?? 0,
            ResponseCount: document.Components?.Responses?.Count ?? 0,
            SecuritySchemeCount: document.Components?.SecuritySchemes?.Count ?? 0,
            RequestBodyCount: document.Components?.RequestBodies?.Count ?? 0,
            DeprecatedOperations: deprecatedOps,
            OperationsWithoutDescription: opsWithoutDesc,
            SchemasWithoutDescription: schemasWithoutDesc,
            MethodsBreakdown: methodsBreakdown,
            LargestSchemas: largestSchemas);
    }

    private static int CountSchemaProperties(OpenApiSchema schema)
    {
        var count = schema.Properties?.Count ?? 0;
        if (schema.Items is not null)
            count += CountSchemaProperties(schema.Items);
        foreach (var s in schema.AllOf ?? [])
            count += CountSchemaProperties(s);
        return count;
    }
}