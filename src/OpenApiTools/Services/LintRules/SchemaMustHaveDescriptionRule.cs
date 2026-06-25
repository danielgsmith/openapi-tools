using Microsoft.OpenApi.Models;
using OpenApiTools.Models;

namespace OpenApiTools.Services.LintRules;

public sealed class SchemaMustHaveDescriptionRule : LintRuleBase
{
    public override string Name => "schema-must-have-description";

    public override IEnumerable<LintFinding> Evaluate(OpenApiDocument document, LintRuleConfig config)
    {
        foreach (var (name, schema) in document.Components?.Schemas ?? new Dictionary<string, OpenApiSchema>())
        {
            if (string.IsNullOrWhiteSpace(schema.Description))
            {
                yield return Finding(config,
                    $"#/components/schemas/{name}",
                    "Schema is missing a description.");
            }
        }
    }
}