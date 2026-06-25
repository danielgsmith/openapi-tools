using Microsoft.OpenApi.Models;
using OpenApiTools.Models;

namespace OpenApiTools.Services.LintRules;

public sealed class OperationMustHaveDescriptionRule : LintRuleBase
{
    public override string Name => "operation-must-have-description";

    public override IEnumerable<LintFinding> Evaluate(OpenApiDocument document, LintRuleConfig config)
    {
        foreach (var (path, item) in document.Paths ?? new OpenApiPaths())
        {
            foreach (var (method, op) in item.Operations)
            {
                if (string.IsNullOrWhiteSpace(op.Description) && string.IsNullOrWhiteSpace(op.Summary))
                {
                    yield return Finding(config,
                        $"{method.ToString().ToUpperInvariant()} {path}",
                        "Operation is missing both description and summary.");
                }
            }
        }
    }
}