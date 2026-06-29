namespace OpenApiTools.Models;

public enum MergeDiagnosticSeverity
{
    Info,
    Warning,
    Error,
}

public sealed record MergeDiagnostic(
    MergeDiagnosticSeverity Severity,
    string Message,
    string? Source = null);

public enum MergeConflictKind
{
    Unique,
    IdenticalDuplicate,
    ConflictingDuplicate,
}

public sealed record MergeComponentConflict(
    MergeConflictKind Kind,
    string ComponentLabel,
    string Name,
    string? ExistingOwner,
    string IncomingOwner);
