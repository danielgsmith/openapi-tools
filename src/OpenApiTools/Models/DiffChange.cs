namespace OpenApiTools.Models;

public enum DiffChangeType
{
    Added,
    Removed,
    Modified,
}

public enum DiffImpact
{
    Breaking,
    NonBreaking,
}

public enum DiffCategory
{
    Path,
    Operation,
    Parameter,
    RequestBody,
    Response,
    Schema,
    SecurityScheme,
}

public sealed record DiffChange(
    DiffChangeType ChangeType,
    DiffImpact Impact,
    DiffCategory Category,
    string Location,
    string Description);