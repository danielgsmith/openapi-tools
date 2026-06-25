using System.ComponentModel;
using OpenApiTools.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace OpenApiTools.Commands;

public sealed class EndpointsCommand : AsyncCommand<EndpointsCommand.Settings>
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
    private readonly IOpenApiEndpointLister _lister;

    public EndpointsCommand(IOpenApiLoader loader, IOpenApiEndpointLister lister)
    {
        _loader = loader;
        _lister = lister;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        try
        {
            var doc = await _loader.LoadAsync(settings.Path);
            var endpoints = _lister.List(doc);

            var output = settings.Format.ToLowerInvariant() switch
            {
                "json" => OutputWriter.ToJson(endpoints),
                "yaml" or "yml" => OutputWriter.ToYaml(endpoints),
                "plain" => OutputWriter.ToPlain(endpoints, e => new[]
                {
                    e.Method,
                    e.Path,
                    e.OperationId ?? "",
                    e.Summary ?? "",
                    string.Join(";", e.Tags),
                    e.Deprecated ? "deprecated" : "",
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
                table.AddColumn("Method");
                table.AddColumn("Path");
                table.AddColumn("OperationId");
                table.AddColumn("Summary");
                table.AddColumn("Tags");
                table.AddColumn("Deprecated");

                foreach (var e in endpoints)
                {
                    table.AddRow(
                        "[blue]" + e.Method + "[/]",
                        e.Path,
                        e.OperationId ?? "",
                        e.Summary ?? "",
                        string.Join(", ", e.Tags),
                        e.Deprecated ? "[red]yes[/]" : "");
                }

                console.Write(table);
                console.MarkupLine("[grey]" + endpoints.Count + " endpoint(s).[/]");
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