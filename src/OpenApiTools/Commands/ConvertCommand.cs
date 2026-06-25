using System.ComponentModel;
using System.Text.Json;
using OpenApiTools.Services;
using SharpYaml;
using Spectre.Console;
using Spectre.Console.Cli;

namespace OpenApiTools.Commands;

public sealed class ConvertCommand : AsyncCommand<ConvertCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<path>")]
        [Description("Path to the OpenAPI document (JSON or YAML).")]
        public string Path { get; init; } = string.Empty;

        [CommandOption("-f|--format <FORMAT>")]
        [Description("Target format: json or yaml.")]
        public string Format { get; init; } = "json";

        [CommandOption("-o|--output <OUTPUT>")]
        [Description("Output file path. If omitted, writes to stdout.")]
        public string? Output { get; init; }
    }

    private readonly IOpenApiLoader _loader;

    public ConvertCommand(IOpenApiLoader loader)
    {
        _loader = loader;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        try
        {
            var document = await _loader.LoadAsync(settings.Path);

            string result = settings.Format.ToLowerInvariant() switch
            {
                "json" => SerializeJson(document),
                "yaml" => SerializeYaml(document),
                _ => throw new InvalidOperationException($"Unknown format: {settings.Format}"),
            };

            if (string.IsNullOrWhiteSpace(settings.Output))
            {
                AnsiConsole.WriteLine(result);
            }
            else
            {
                await File.WriteAllTextAsync(settings.Output, result);
                AnsiConsole.MarkupLine($"[green]✓[/] Wrote '{settings.Output}'.");
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    private static string SerializeJson(object document)
    {
        return JsonSerializer.Serialize(document, new JsonSerializerOptions
        {
            WriteIndented = true,
        });
    }

    private static string SerializeYaml(object document)
    {
        var serializer = new SharpYaml.Serialization.Serializer();
        return serializer.Serialize(document);
    }
}