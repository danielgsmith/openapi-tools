using System.ComponentModel;
using OpenApiTools.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace OpenApiTools.Commands;

public sealed class SearchCommand : AsyncCommand<SearchCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<path>")]
        [Description("Path to the OpenAPI document.")]
        public string Path { get; init; } = string.Empty;

        [CommandArgument(1, "<query>")]
        [Description("Search query (fuzzy match).")]
        public string Query { get; init; } = string.Empty;

        [CommandOption("-c|--components <COMPONENTS>")]
        [Description("Comma-separated components to search: endpoints,schemas,parameters,responses,security (default: all).")]
        public string? Components { get; init; }

        [CommandOption("-t|--threshold <THRESHOLD>")]
        [Description("Match threshold 0.0-1.0 (default: 0.4).")]
        public double? Threshold { get; init; }

        [CommandOption("-f|--format <FORMAT>")]
        [Description("Output format: table, json, yaml, plain (default: table).")]
        public string Format { get; init; } = "table";

        [CommandOption("-o|--output <OUTPUT>")]
        [Description("Write results to a file instead of stdout.")]
        public string? Output { get; init; }
    }

    private readonly IOpenApiLoader _loader;
    private readonly IOpenApiSearcher _searcher;

    public SearchCommand(IOpenApiLoader loader, IOpenApiSearcher searcher)
    {
        _loader = loader;
        _searcher = searcher;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        try
        {
            var doc = await _loader.LoadAsync(settings.Path);

            var components = string.IsNullOrWhiteSpace(settings.Components)
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "endpoints", "schemas", "parameters", "responses", "security" }
                : new HashSet<string>(
                    settings.Components.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                    StringComparer.OrdinalIgnoreCase);

            var threshold = settings.Threshold ?? 0.4;
            var results = _searcher.Search(doc, settings.Query, components, threshold);

            if (results.Count == 0)
            {
                if (string.IsNullOrWhiteSpace(settings.Output))
                    AnsiConsole.MarkupLine("[yellow]No matches found.[/]");
                return 0;
            }

            var output = settings.Format.ToLowerInvariant() switch
            {
                "json" => OutputWriter.ToJson(results),
                "yaml" or "yml" => OutputWriter.ToYaml(results),
                "plain" => OutputWriter.ToPlain(results, r => new[]
                {
                    r.Component,
                    r.Name,
                    r.Path,
                    r.Description ?? "",
                    r.Score.ToString("0.00"),
                }),
                _ => null,
            };

            if (output is not null)
            {
                await OutputWriter.WriteOutputAsync(settings.Output, output);
                return 0;
            }

            await OutputWriter.WriteOutputAsync(settings.Output, console =>
            {
                var table = new Table();
                table.AddColumn("Component");
                table.AddColumn("Name");
                table.AddColumn("Location");
                table.AddColumn("Description");
                table.AddColumn("Score");

                foreach (var r in results)
                {
                    table.AddRow(
                        r.Component,
                        r.Name,
                        r.Path,
                        r.Description ?? "",
                        r.Score.ToString("0.00"));
                }

                console.Write(table);
                console.MarkupLine("[grey]" + results.Count + " match(es).[/]");
            });

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }
}