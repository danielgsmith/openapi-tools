using System.ComponentModel;
using OpenApiTools.Models;
using OpenApiTools.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace OpenApiTools.Commands;

public sealed class LintCommand : AsyncCommand<LintCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<spec>")]
        [Description("Path to the OpenAPI document.")]
        public string SpecPath { get; init; } = string.Empty;

        [CommandArgument(1, "<config>")]
        [Description("Path to the lint config YAML file.")]
        public string ConfigPath { get; init; } = string.Empty;

        [CommandOption("-f|--format <FORMAT>")]
        [Description("Output format: table, json, yaml, plain (default: table).")]
        public string Format { get; init; } = "table";

        [CommandOption("-o|--output <OUTPUT>")]
        [Description("Write results to a file instead of stdout.")]
        public string? Output { get; init; }
    }

    private readonly IOpenApiLoader _loader;
    private readonly ILinter _linter;

    public LintCommand(IOpenApiLoader loader, ILinter linter)
    {
        _loader = loader;
        _linter = linter;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        try
        {
            var doc = await _loader.LoadAsync(settings.SpecPath);
            var config = LintConfigLoader.Load(settings.ConfigPath);
            var findings = _linter.Lint(doc, config);

            var errors = findings.Count(f => f.Severity == LintSeverity.Error);
            var warnings = findings.Count(f => f.Severity == LintSeverity.Warning);

            if (findings.Count == 0)
            {
                if (string.IsNullOrWhiteSpace(settings.Output))
                    AnsiConsole.MarkupLine("[green]No lint issues found.[/]");
                return errors > 0 ? 1 : 0;
            }

            var output = settings.Format.ToLowerInvariant() switch
            {
                "json" => OutputWriter.ToJson(findings),
                "yaml" or "yml" => OutputWriter.ToYaml(findings),
                "plain" => OutputWriter.ToPlain(findings, f => new[]
                {
                    f.Severity.ToString(),
                    f.Rule,
                    f.Location,
                    f.Message,
                }),
                _ => null,
            };

            if (output is not null)
            {
                await OutputWriter.WriteOutputAsync(settings.Output, output);
                return errors > 0 ? 1 : 0;
            }

            await OutputWriter.WriteOutputAsync(settings.Output, console =>
            {
                var table = new Table();
                table.AddColumn("Severity");
                table.AddColumn("Rule");
                table.AddColumn("Location");
                table.AddColumn("Message");

                foreach (var f in findings)
                {
                    var severityMarkup = f.Severity switch
                    {
                        LintSeverity.Error => "[red]error[/]",
                        LintSeverity.Warning => "[yellow]warning[/]",
                        _ => "[grey]info[/]",
                    };
                    table.AddRow(severityMarkup, f.Rule, f.Location, f.Message);
                }

                console.Write(table);
                console.MarkupLine("[grey]" + findings.Count + " finding(s): " + errors + " error(s), " + warnings + " warning(s).[/]");
            });

            return errors > 0 ? 1 : 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }
}