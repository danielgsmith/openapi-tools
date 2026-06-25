using System.ComponentModel;
using OpenApiTools.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace OpenApiTools.Commands;

public sealed class StatsCommand : AsyncCommand<StatsCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<path>")]
        [Description("Path to the OpenAPI document.")]
        public string Path { get; init; } = string.Empty;

        [CommandOption("-o|--output <OUTPUT>")]
        [Description("Write output to a file instead of stdout.")]
        public string? Output { get; init; }
    }

    private readonly IOpenApiLoader _loader;
    private readonly IOpenApiStatsService _stats;

    public StatsCommand(IOpenApiLoader loader, IOpenApiStatsService stats)
    {
        _loader = loader;
        _stats = stats;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        try
        {
            var doc = await _loader.LoadAsync(settings.Path);
            var s = _stats.Compute(doc);

            await OutputWriter.WriteOutputAsync(settings.Output, console =>
            {
                var panel = new Table().RoundedBorder();
                panel.AddColumn("Metric");
                panel.AddColumn("Value");
                panel.AddRow("Paths", s.PathCount.ToString());
                panel.AddRow("Operations", s.OperationCount.ToString());
                panel.AddRow("Schemas", s.SchemaCount.ToString());
                panel.AddRow("Parameters (components)", s.ParameterCount.ToString());
                panel.AddRow("Responses (components)", s.ResponseCount.ToString());
                panel.AddRow("Request Bodies (components)", s.RequestBodyCount.ToString());
                panel.AddRow("Security Schemes", s.SecuritySchemeCount.ToString());
                panel.AddRow("Deprecated Operations", "[red]" + s.DeprecatedOperations + "[/]");
                panel.AddRow("Ops without description", "[yellow]" + s.OperationsWithoutDescription + "[/]");
                panel.AddRow("Schemas without description", "[yellow]" + s.SchemasWithoutDescription + "[/]");

                console.Write(panel);

                if (s.MethodsBreakdown.Count > 0)
                {
                    var methodChart = new BarChart();
                    foreach (var (method, count) in s.MethodsBreakdown.OrderByDescending(x => x.Value))
                        methodChart.AddItem(method, count, Color.Blue);
                    console.Write(methodChart);
                }

                if (s.LargestSchemas.Count > 0)
                {
                    console.MarkupLine("[bold]Largest schemas:[/]");
                    foreach (var schema in s.LargestSchemas)
                        console.MarkupLine("  " + schema);
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