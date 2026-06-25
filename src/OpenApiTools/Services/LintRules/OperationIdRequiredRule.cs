using Microsoft.OpenApi.Models;
using OpenApiTools.Models;

namespace OpenApiTools.Services.LintRules;

public sealed class OperationIdRequiredRule : LintRuleBase
{
    public override string Name => "operationid-required";

    public override IEnumerable<LintFinding> Evaluate(OpenApiDocument document, LintRuleConfig config)
    {
        foreach (var (path, item) in document.Paths ?? new OpenApiPaths())
        {
            foreach (var (method, op) in item.Operations)
            {
                if (string.IsNullOrWhiteSpace(op.OperationId))
                {
                    yield return Finding(config,
                        $"{method.ToString().ToUpperInvariant()} {path}",
                        "Operation is missing an operationId.");
                }
            }
        }
    }
}