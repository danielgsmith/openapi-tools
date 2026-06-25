using System.ComponentModel;
using OpenApiTools.Models;
using OpenApiTools.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace OpenApiTools.Commands;

public sealed class DiffCommand : AsyncCommand<DiffCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<old>")]
        [Description("Path to the old (baseline) OpenAPI document.")]
        public string OldPath { get; init; } = string.Empty;

        [CommandArgument(1, "<new>")]
        [Description("Path to the new (modified) OpenAPI document.")]
        public string NewPath { get; init; } = string.Empty;

        [CommandOption("--breaking-only")]
        [Description("Show only breaking changes.")]
        public bool BreakingOnly { get; init; }

        [CommandOption("-f|--format <FORMAT>")]
        [Description("Output format: table, json, yaml, plain (default: table).")]
        public string Format { get; init; } = "table";

        [CommandOption("-o|--output <OUTPUT>")]
        [Description("Write results to a file instead of stdout.")]
        public string? Output { get; init; }
    }

    private readonly IOpenApiLoader _loader;
    private readonly IOpenApiDiffer _differ;

    public DiffCommand(IOpenApiLoader loader, IOpenApiDiffer differ)
    {
        _loader = loader;
        _differ = differ;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        try
        {
            var oldDoc = await _loader.LoadAsync(settings.OldPath);
            var newDoc = await _loader.LoadAsync(settings.NewPath);
            var changes = _differ.Diff(oldDoc, newDoc);

            if (settings.BreakingOnly)
                changes = changes.Where(c => c.Impact == DiffImpact.Breaking).ToList();

            var breakingCount = changes.Count(c => c.Impact == DiffImpact.Breaking);

            if (changes.Count == 0)
            {
                if (string.IsNullOrWhiteSpace(settings.Output))
                    AnsiConsole.MarkupLine("[green]No changes detected.[/]");
                return breakingCount > 0 ? 1 : 0;
            }

            var output = settings.Format.ToLowerInvariant() switch
            {
                "json" => OutputWriter.ToJson(changes),
                "yaml" or "yml" => OutputWriter.ToYaml(changes),
                "plain" => OutputWriter.ToPlain(changes, c => new[]
                {
                    c.ChangeType.ToString(),
                    c.Impact.ToString(),
                    c.Category.ToString(),
                    c.Location,
                    c.Description,
                }),
                _ => null,
            };

            if (output is not null)
            {
                await OutputWriter.WriteOutputAsync(settings.Output, output);
                return breakingCount > 0 ? 1 : 0;
            }

            await OutputWriter.WriteOutputAsync(settings.Output, console =>
            {
                var table = new Table();
                table.AddColumn("Change");
                table.AddColumn("Impact");
                table.AddColumn("Category");
                table.AddColumn("Location");
                table.AddColumn("Description");

                foreach (var c in changes)
                {
                    var changeMarkup = c.ChangeType switch
                    {
                        DiffChangeType.Added => "[green]+ added[/]",
                        DiffChangeType.Removed => "[red]- removed[/]",
                        _ => "[yellow]~ modified[/]",
                    };
                    var impactMarkup = c.Impact == DiffImpact.Breaking
                        ? "[red]BREAKING[/]"
                        : "[grey]non-breaking[/]";

                    table.AddRow(changeMarkup, impactMarkup, c.Category.ToString(), c.Location, c.Description);
                }

                console.Write(table);

                var summary = "[grey]" + changes.Count + " change(s)";
                if (breakingCount > 0)
                    summary += ", [red]" + breakingCount + " breaking[/]";
                summary += ".[/]";
                console.MarkupLine(summary);
            });

            return breakingCount > 0 ? 1 : 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }
}