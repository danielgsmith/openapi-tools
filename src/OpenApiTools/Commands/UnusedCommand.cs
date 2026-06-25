using System.ComponentModel;
using OpenApiTools.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace OpenApiTools.Commands;

public sealed class UnusedCommand : AsyncCommand<UnusedCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<path>")]
        [Description("Path to the OpenAPI document.")]
        public string Path { get; init; } = string.Empty;

        [CommandOption("-f|--format <FORMAT>")]
        [Description("Output format: table, json, yaml, plain (default: table).")]
        public string Format { get; init; } = "table";

        [CommandOption("-o|--output <OUTPUT>")]
        [Description("Write results to a file instead of stdout.")]
        public string? Output { get; init; }
    }

    private readonly IOpenApiLoader _loader;
    private readonly IOpenApiUnusedFinder _finder;

    public UnusedCommand(IOpenApiLoader loader, IOpenApiUnusedFinder finder)
    {
        _loader = loader;
        _finder = finder;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        try
        {
            var doc = await _loader.LoadAsync(settings.Path);
            var unused = _finder.Find(doc);

            if (unused.Count == 0)
            {
                if (string.IsNullOrWhiteSpace(settings.Output))
                    AnsiConsole.MarkupLine("[green]No unused components found.[/]");
                return 0;
            }

            var output = settings.Format.ToLowerInvariant() switch
            {
                "json" => OutputWriter.ToJson(unused),
                "yaml" or "yml" => OutputWriter.ToYaml(unused),
                "plain" => OutputWriter.ToPlain(unused, u => new[]
                {
                    u.Type.ToString(),
                    u.Name,
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
                table.AddColumn("Type");
                table.AddColumn("Name");

                foreach (var u in unused)
                    table.AddRow(u.Type.ToString(), u.Name);

                console.Write(table);
                console.MarkupLine("[grey]" + unused.Count + " unused component(s).[/]");
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