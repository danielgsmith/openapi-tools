namespace OpenApiTools.Models;

public enum LintSeverity
{
    Error,
    Warning,
    Info,
}

public sealed record LintFinding(
    string Rule,
    LintSeverity Severity,
    string Location,
    string Message);