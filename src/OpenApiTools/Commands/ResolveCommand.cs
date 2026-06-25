using System.ComponentModel;
using OpenApiTools.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace OpenApiTools.Commands;

public sealed class ResolveCommand : AsyncCommand<ResolveCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<path>")]
        [Description("Path to the OpenAPI document.")]
        public string Path { get; init; } = string.Empty;

        [CommandOption("-f|--format <FORMAT>")]
        [Description("Output format: json or yaml (default: yaml).")]
        public string Format { get; init; } = "yaml";

        [CommandOption("-o|--output <OUTPUT>")]
        [Description("Output file path. If omitted, writes to stdout.")]
        public string? Output { get; init; }
    }

    private readonly IOpenApiLoader _loader;
    private readonly IOpenApiResolver _resolver;

    public ResolveCommand(IOpenApiLoader loader, IOpenApiResolver resolver)
    {
        _loader = loader;
        _resolver = resolver;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        try
        {
            var doc = await _loader.LoadAsync(settings.Path);
            var content = _resolver.ResolveAndSerialize(doc, settings.Format);

            if (string.IsNullOrWhiteSpace(settings.Output))
            {
                AnsiConsole.WriteLine(content);
            }
            else
            {
                await File.WriteAllTextAsync(settings.Output, content);
                AnsiConsole.MarkupLine("[green]Resolved document written to '" + settings.Output + "'.[/]");
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }
}