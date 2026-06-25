using OpenApiTools.Models;
using OpenApiTools.Services;
using Xunit;

namespace OpenApiTools.Tests;

public class LinterTests
{
    private static string SpecPath => Path.Combine(AppContext.BaseDirectory, "samples", "store-api-v2.yaml");
    private static string ConfigPath => Path.Combine(AppContext.BaseDirectory, "samples", "lint-config.yaml");

    [Fact]
    public async Task Lint_Should_Find_Missing_Descriptions()
    {
        var loader = new OpenApiLoader();
        var doc = await loader.LoadAsync(SpecPath);
        var linter = new Linter();
        var config = LintConfigLoader.Load(ConfigPath);

        var findings = linter.Lint(doc, config);

        Assert.Contains(findings, f => f.Rule == "operation-must-have-description");
    }

    [Fact]
    public async Task Lint_Should_Find_Missing_OperationIds()
    {
        var loader = new OpenApiLoader();
        var doc = await loader.LoadAsync(SpecPath);
        var linter = new Linter();
        var config = LintConfigLoader.Load(ConfigPath);

        var findings = linter.Lint(doc, config);

        var opIdFindings = findings.Where(f => f.Rule == "operationid-required");
        Assert.True(opIdFindings.Count() >= 1, "Should find ops missing operationId");
    }

    [Fact]
    public async Task Lint_Should_Find_Schemas_Without_Description()
    {
        var loader = new OpenApiLoader();
        var doc = await loader.LoadAsync(SpecPath);
        var linter = new Linter();
        var config = LintConfigLoader.Load(ConfigPath);

        var findings = linter.Lint(doc, config);

        var schemaFindings = findings.Where(f => f.Rule == "schema-must-have-description");
        Assert.True(schemaFindings.Count() >= 1, "Should find schemas missing description");
    }

    [Fact]
    public async Task Lint_Should_Find_Missing_4xx_Responses()
    {
        var loader = new OpenApiLoader();
        var doc = await loader.LoadAsync(SpecPath);
        var linter = new Linter();
        var config = LintConfigLoader.Load(ConfigPath);

        var findings = linter.Lint(doc, config);

        var responseFindings = findings.Where(f => f.Rule == "all-4xx-responses");
        Assert.True(responseFindings.Count() >= 1, "Should find ops missing 4xx responses");
    }

    [Fact]
    public async Task Lint_Should_Report_Severities()
    {
        var loader = new OpenApiLoader();
        var doc = await loader.LoadAsync(SpecPath);
        var linter = new Linter();
        var config = LintConfigLoader.Load(ConfigPath);

        var findings = linter.Lint(doc, config);

        Assert.Contains(findings, f => f.Severity == LintSeverity.Error);
    }

    [Fact]
    public void LintConfigLoader_Should_Parse_Yaml()
    {
        var config = LintConfigLoader.Load(ConfigPath);

        Assert.True(config.Rules.Count >= 5);
        Assert.Contains("operation-must-have-description", config.Rules.Keys);
        Assert.Contains("all-4xx-responses", config.Rules.Keys);
    }
}