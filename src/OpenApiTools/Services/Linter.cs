using Microsoft.OpenApi.Models;
using OpenApiTools.Models;
using OpenApiTools.Services.LintRules;

namespace OpenApiTools.Services;

public interface ILinter
{
    IReadOnlyList<LintFinding> Lint(OpenApiDocument document, LintConfig config);
}

public sealed class Linter : ILinter
{
    private readonly Dictionary<string, ILintRule> _rules;

    public Linter()
    {
        _rules = new LintRuleFactory().CreateAll();
    }

    public IReadOnlyList<LintFinding> Lint(OpenApiDocument document, LintConfig config)
    {
        var findings = new List<LintFinding>();

        foreach (var (ruleName, ruleConfig) in config.Rules)
        {
            if (!_rules.TryGetValue(ruleName, out var rule))
            {
                findings.Add(new LintFinding(
                    Rule: "(config)",
                    Severity: LintSeverity.Warning,
                    Location: "",
                    Message: "Unknown rule: '" + ruleName + "'."));
                continue;
            }

            findings.AddRange(rule.Evaluate(document, ruleConfig));
        }

        return findings;
    }
}