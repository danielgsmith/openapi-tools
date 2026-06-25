using System.ComponentModel;
using OpenApiTools.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace OpenApiTools.Commands;

public sealed class DescribeSchemaCommand : AsyncCommand<DescribeSchemaCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<path>")]
        [Description("Path to the OpenAPI document.")]
        public string FilePath { get; init; } = string.Empty;

        [CommandArgument(1, "<schema>")]
        [Description("Name of the schema to describe.")]
        public string SchemaName { get; init; } = string.Empty;

        [CommandOption("-o|--output <OUTPUT>")]
        [Description("Write output to a file instead of stdout.")]
        public string? Output { get; init; }
    }

    private readonly IOpenApiLoader _loader;
    private readonly IOpenApiDescriber _describer;

    public DescribeSchemaCommand(IOpenApiLoader loader, IOpenApiDescriber describer)
    {
        _loader = loader;
        _describer = describer;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        try
        {
            var doc = await _loader.LoadAsync(settings.FilePath);
            var detail = _describer.DescribeSchema(doc, settings.SchemaName);

            await OutputWriter.WriteOutputAsync(settings.Output, console =>
            {
                console.Write(new Panel("[bold]Schema: " + detail.Name + "[/]")
                    .Header("Schema"));

                if (detail.Deprecated)
                    console.MarkupLine("[red]This schema is deprecated.[/]");

                if (!string.IsNullOrEmpty(detail.Type))
                    console.MarkupLine("[bold]Type:[/] " + detail.Type);
                if (!string.IsNullOrEmpty(detail.Format))
                    console.MarkupLine("[bold]Format:[/] " + detail.Format);
                if (!string.IsNullOrEmpty(detail.Description))
                    console.MarkupLine("[bold]Description:[/] " + detail.Description);

                if (detail.Required.Count > 0)
                    console.MarkupLine("[bold]Required:[/] " + string.Join(", ", detail.Required));

                if (detail.Properties.Count > 0)
                {
                    var propTable = new Table().RoundedBorder();
                    propTable.AddColumn("Name");
                    propTable.AddColumn("Type");
                    propTable.AddColumn("Format");
                    propTable.AddColumn("Required");
                    propTable.AddColumn("Description");
                    foreach (var p in detail.Properties)
                        propTable.AddRow(p.Name, p.Type ?? "", p.Format ?? "", p.Required ? "[red]yes[/]" : "no", p.Description ?? "");
                    console.Write(propTable);
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