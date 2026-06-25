using Microsoft.OpenApi.Models;
using OpenApiTools.Models;

namespace OpenApiTools.Services.LintRules;

public sealed class ParamMustHaveDescriptionRule : LintRuleBase
{
    public override string Name => "param-must-have-description";

    public override IEnumerable<LintFinding> Evaluate(OpenApiDocument document, LintRuleConfig config)
    {
        foreach (var (path, item) in document.Paths ?? new OpenApiPaths())
        {
            foreach (var (method, op) in item.Operations)
            {
                foreach (var param in op.Parameters ?? [])
                {
                    if (string.IsNullOrWhiteSpace(param.Description))
                    {
                        yield return Finding(config,
                            $"{method.ToString().ToUpperInvariant()} {path} param:{param.Name}",
                            $"Parameter '{param.Name}' is missing a description.");
                    }
                }
            }
        }
    }
}