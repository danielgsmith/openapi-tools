using Microsoft.OpenApi.Models;
using OpenApiTools.Models;

namespace OpenApiTools.Services.LintRules;

public sealed class NoGetWithBodyRule : LintRuleBase
{
    public override string Name => "no-get-with-body";

    public override IEnumerable<LintFinding> Evaluate(OpenApiDocument document, LintRuleConfig config)
    {
        foreach (var (path, item) in document.Paths ?? new OpenApiPaths())
        {
            if (item.Operations.TryGetValue(OperationType.Get, out var getOp) && getOp.RequestBody is not null)
            {
                yield return Finding(config,
                    $"GET {path}",
                    "GET operation should not have a request body.");
            }
        }
    }
}