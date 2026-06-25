using System.ComponentModel;
using OpenApiTools.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace OpenApiTools.Commands;

public sealed class SplitCommand : AsyncCommand<SplitCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<path>")]
        [Description("Path to the OpenAPI document.")]
        public string Path { get; init; } = string.Empty;

        [CommandArgument(1, "<output>")]
        [Description("Output directory for the split files.")]
        public string OutputDir { get; init; } = string.Empty;
    }

    private readonly IOpenApiLoader _loader;
    private readonly IOpenApiSplitter _splitter;

    public SplitCommand(IOpenApiLoader loader, IOpenApiSplitter splitter)
    {
        _loader = loader;
        _splitter = splitter;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        try
        {
            var doc = await _loader.LoadAsync(settings.Path);
            await _splitter.SplitAsync(doc, settings.OutputDir);

            var fileCount = Directory.GetFiles(settings.OutputDir, "*", SearchOption.AllDirectories).Length;
            AnsiConsole.MarkupLine("[green]Split into " + fileCount + " files in '" + settings.OutputDir + "'.[/]");

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }
}