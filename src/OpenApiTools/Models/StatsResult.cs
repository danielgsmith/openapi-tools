namespace OpenApiTools.Models;

public sealed record StatsResult(
    int PathCount,
    int OperationCount,
    int SchemaCount,
    int ParameterCount,
    int ResponseCount,
    int SecuritySchemeCount,
    int RequestBodyCount,
    int DeprecatedOperations,
    int OperationsWithoutDescription,
    int SchemasWithoutDescription,
    Dictionary<string, int> MethodsBreakdown,
    List<string> LargestSchemas);