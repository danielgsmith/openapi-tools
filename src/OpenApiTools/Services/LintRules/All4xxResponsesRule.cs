using Microsoft.OpenApi.Models;
using OpenApiTools.Models;

namespace OpenApiTools.Services.LintRules;

public sealed class All4xxResponsesRule : LintRuleBase
{
    public override string Name => "all-4xx-responses";

    public override IEnumerable<LintFinding> Evaluate(OpenApiDocument document, LintRuleConfig config)
    {
        var codes = new HashSet<string> { "400", "404" };
        if (config.Options is not null && config.Options.TryGetValue("codes", out var codesObj))
        {
            codes = new HashSet<string>(
                codesObj switch
                {
                    IEnumerable<object> list => list.Select(o => o?.ToString() ?? ""),
                    _ => [codesObj?.ToString() ?? ""],
                },
                StringComparer.OrdinalIgnoreCase);
        }

        foreach (var (path, item) in document.Paths ?? new OpenApiPaths())
        {
            foreach (var (method, op) in item.Operations)
            {
                var responseCodes = (op.Responses ?? new Dictionary<string, OpenApiResponse>()).Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var expected in codes)
                {
                    if (!responseCodes.Contains(expected))
                    {
                        yield return Finding(config,
                            $"{method.ToString().ToUpperInvariant()} {path}",
                            $"Operation is missing '{expected}' response.");
                    }
                }
            }
        }
    }
}