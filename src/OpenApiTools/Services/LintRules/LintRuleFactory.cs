namespace OpenApiTools.Services.LintRules;

public sealed class LintRuleFactory
{
    public Dictionary<string, ILintRule> CreateAll() => new()
    {
        [new OperationMustHaveDescriptionRule().Name] = new OperationMustHaveDescriptionRule(),
        [new OperationIdRequiredRule().Name] = new OperationIdRequiredRule(),
        [new NoGetWithBodyRule().Name] = new NoGetWithBodyRule(),
        [new All4xxResponsesRule().Name] = new All4xxResponsesRule(),
        [new SchemaMustHaveDescriptionRule().Name] = new SchemaMustHaveDescriptionRule(),
        [new ParamMustHaveDescriptionRule().Name] = new ParamMustHaveDescriptionRule(),
    };
}