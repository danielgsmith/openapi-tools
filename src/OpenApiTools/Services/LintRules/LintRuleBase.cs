using Microsoft.OpenApi.Models;
using OpenApiTools.Models;

namespace OpenApiTools.Services;

public interface ILintRule
{
    string Name { get; }
    IEnumerable<LintFinding> Evaluate(OpenApiDocument document, LintRuleConfig config);
}

public abstract class LintRuleBase : ILintRule
{
    public abstract string Name { get; }

    public abstract IEnumerable<LintFinding> Evaluate(OpenApiDocument document, LintRuleConfig config);

    protected static LintSeverity ParseSeverity(string severity) => severity.ToLowerInvariant() switch
    {
        "error" => LintSeverity.Error,
        "warning" => LintSeverity.Warning,
        "info" => LintSeverity.Info,
        _ => LintSeverity.Error,
    };

    protected LintFinding Finding(LintRuleConfig config, string location, string message)
        => new(Name, ParseSeverity(config.Severity), location, message);
}