using System.ComponentModel;
using OpenApiTools.Models;
using OpenApiTools.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace OpenApiTools.Commands;

public sealed class DiscoverCommand : AsyncCommand<DiscoverCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<path>")]
        [Description("Directory to scan recursively for OpenAPI documents.")]
        public string Path { get; init; } = string.Empty;

        [CommandOption("-e|--ext <EXTENSIONS>")]
        [Description("Comma-separated list of file extensions to consider (default: json,yaml,yml).")]
        public string? Extensions { get; init; }

        [CommandOption("-s|--max-size <MB>")]
        [Description("Skip files larger than this many megabytes (default: 10).")]
        public int? MaxSizeMb { get; init; }

        [CommandOption("--no-validate")]
        [Description("Skip full parse validation; use only the cheap magic-string prefilter.")]
        public bool NoValidate { get; init; }

        [CommandOption("-f|--format <FORMAT>")]
        [Description("Output format: table, json, yaml, plain (default: table).")]
        public string Format { get; init; } = "table";

        [CommandOption("-o|--output <OUTPUT>")]
        [Description("Write results to a file instead of stdout.")]
        public string? Output { get; init; }
    }

    private readonly IOpenApiDiscoverer _discoverer;

    public DiscoverCommand(IOpenApiDiscoverer discoverer)
    {
        _discoverer = discoverer;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        try
        {
            var options = BuildOptions(settings);
            var results = await _discoverer.DiscoverAsync(settings.Path, options);

            var output = settings.Format.ToLowerInvariant() switch
            {
                "json" => OutputWriter.ToJson(results),
                "yaml" or "yml" => OutputWriter.ToYaml(results),
                "plain" => OutputWriter.ToPlain(results, r => new[]
                {
                    r.Path,
                    r.Status.ToString(),
                    r.SpecVersion ?? "",
                    r.DocumentVersion ?? "",
                    r.Title ?? "",
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
                table.AddColumn("Path");
                table.AddColumn("Status");
                table.AddColumn("Spec");
                table.AddColumn("Doc Version");
                table.AddColumn("Title");

                foreach (var r in results)
                {
                    var status = r.Status switch
                    {
                        DiscoveryStatus.Valid => "[green]" + r.Status + "[/]",
                        DiscoveryStatus.Invalid => "[red]" + r.Status + "[/]",
                        _ => r.Status.ToString(),
                    };
                    table.AddRow(
                        r.Path,
                        status,
                        r.SpecVersion ?? "",
                        r.DocumentVersion ?? "",
                        r.Title ?? "");
                }

                console.Write(table);
                console.MarkupLine("[grey]Found " + results.Count + " OpenAPI document(s).[/]");
            });
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    private static DiscoverOptions BuildOptions(Settings settings)
    {
        var extensions = DiscoverOptions.Default.Extensions;
        if (!string.IsNullOrWhiteSpace(settings.Extensions))
        {
            extensions = new HashSet<string>(
                settings.Extensions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(e => e.StartsWith('.') ? e.ToLowerInvariant() : "." + e.ToLowerInvariant()),
                StringComparer.OrdinalIgnoreCase);
        }

        var maxBytes = (settings.MaxSizeMb ?? 10) * 1024L * 1024L;
        return new DiscoverOptions(extensions, maxBytes, !settings.NoValidate);
    }

}