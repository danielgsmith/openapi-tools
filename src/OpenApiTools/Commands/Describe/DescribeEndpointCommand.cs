using System.ComponentModel;
using OpenApiTools.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace OpenApiTools.Commands;

public sealed class DescribeEndpointCommand : AsyncCommand<DescribeEndpointCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<path>")]
        [Description("Path to the OpenAPI document.")]
        public string FilePath { get; init; } = string.Empty;

        [CommandArgument(1, "<endpoint>")]
        [Description("Endpoint in format 'METHOD /path', e.g. 'GET /pets'.")]
        public string Endpoint { get; init; } = string.Empty;

        [CommandOption("-o|--output <OUTPUT>")]
        [Description("Write output to a file instead of stdout.")]
        public string? Output { get; init; }
    }

    private readonly IOpenApiLoader _loader;
    private readonly IOpenApiDescriber _describer;

    public DescribeEndpointCommand(IOpenApiLoader loader, IOpenApiDescriber describer)
    {
        _loader = loader;
        _describer = describer;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        try
        {
            var doc = await _loader.LoadAsync(settings.FilePath);
            var detail = _describer.DescribeEndpoint(doc, settings.Endpoint);

            await OutputWriter.WriteOutputAsync(settings.Output, console =>
            {
                var opId = string.IsNullOrEmpty(detail.OperationId) ? "(no operationId)" : detail.OperationId;
                console.Write(new Panel("[bold]" + detail.Method + " " + detail.Path + "[/]")
                    .Header("Endpoint: " + opId));

                if (detail.Deprecated)
                    console.MarkupLine("[red]This operation is deprecated.[/]");

                if (!string.IsNullOrEmpty(detail.Summary))
                    console.MarkupLine("[bold]Summary:[/] " + detail.Summary);
                if (!string.IsNullOrEmpty(detail.Description))
                    console.MarkupLine("[bold]Description:[/] " + detail.Description);
                if (detail.Tags.Count > 0)
                    console.MarkupLine("[bold]Tags:[/] " + string.Join(", ", detail.Tags));

                if (detail.Parameters.Count > 0)
                {
                    var paramTable = new Table().RoundedBorder();
                    paramTable.AddColumn("Name");
                    paramTable.AddColumn("In");
                    paramTable.AddColumn("Required");
                    paramTable.AddColumn("Type");
                    paramTable.AddColumn("Description");
                    foreach (var p in detail.Parameters)
                        paramTable.AddRow(p.Name, p.In, p.Required ? "[red]yes[/]" : "no", p.Type ?? "", p.Description ?? "");
                    console.Write(paramTable);
                }

                if (detail.RequestBody is not null)
                {
                    console.MarkupLine("[bold]Request Body:[/] required=" + detail.RequestBody.Required);
                    if (!string.IsNullOrEmpty(detail.RequestBody.Description))
                        console.MarkupLine("  " + detail.RequestBody.Description);
                    console.MarkupLine("  Content types: " + string.Join(", ", detail.RequestBody.ContentTypes));
                }

                if (detail.Responses.Count > 0)
                {
                    var respTable = new Table().RoundedBorder();
                    respTable.AddColumn("Status");
                    respTable.AddColumn("Description");
                    respTable.AddColumn("Content types");
                    foreach (var r in detail.Responses)
                        respTable.AddRow(r.StatusCode, r.Description ?? "", string.Join(", ", r.ContentTypes));
                    console.Write(respTable);
                }
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