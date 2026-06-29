using System.ComponentModel;
using OpenApiTools.Models;
using OpenApiTools.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace OpenApiTools.Commands;

public sealed class MergeCommand : AsyncCommand<MergeCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[files...]")]
        [Description("OpenAPI files to merge (required unless --config is used).")]
        public string[]? Files { get; init; }

        [CommandOption("--config <CONFIG>")]
        [Description("Path to a JSON merge configuration file.")]
        public string? Config { get; init; }

        [CommandOption("-o|--output <OUTPUT>")]
        [Description("Output file path (default: merged-openapi.json).")]
        public string? Output { get; init; }

        [CommandOption("--title <TITLE>")]
        [Description("API title for the merged specification.")]
        public string? Title { get; init; }

        [CommandOption("--version <VERSION>")]
        [Description("API version for the merged specification.")]
        public string? Version { get; init; }

        [CommandOption("--schema-conflict <STRATEGY>")]
        [Description("Schema conflict strategy: keep-existing, keep-incoming, rename-incoming, or fail.")]
        public string? SchemaConflict { get; init; }

        [CommandOption("--schema-identical <STRATEGY>")]
        [Description("Schema duplicate strategy for identical definitions: dedupe, warn-and-dedupe, or fail.")]
        public string? SchemaIdentical { get; init; }

        [CommandOption("-f|--format <FORMAT>")]
        [Description("Output format: json or yaml (default: json).")]
        public string Format { get; init; } = "json";

        [CommandOption("-v|--verbose")]
        [Description("Show detailed progress and warnings.")]
        public bool Verbose { get; init; }
    }

    private readonly IOpenApiLoader _loader;
    private readonly IOpenApiMerger _merger;
    private readonly IOpenApiSerializer _serializer;

    public MergeCommand(IOpenApiLoader loader, IOpenApiMerger merger, IOpenApiSerializer serializer)
    {
        _loader = loader;
        _merger = merger;
        _serializer = serializer;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        try
        {
            var (config, sourcePaths) = settings.Config is not null
                ? LoadFromConfig(settings.Config)
                : BuildFromArgs(settings);

            if (sourcePaths.Count == 0)
            {
                AnsiConsole.MarkupLine("[red]ERROR: No source files specified.[/]");
                return 1;
            }

            if (settings.Verbose)
                AnsiConsole.MarkupLine("[grey]Loading " + sourcePaths.Count + " source file(s)...[/]");

            var sources = new List<(SourceConfiguration, Microsoft.OpenApi.Models.OpenApiDocument)>();
            foreach (var sourcePath in sourcePaths)
            {
                if (settings.Verbose)
                    AnsiConsole.MarkupLine("[grey]  Loading: " + sourcePath + "[/]");

                var doc = await _loader.LoadAsync(sourcePath);
                var sourceConfig = config.Sources.FirstOrDefault(s => s.Path == sourcePath)
                    ?? new SourceConfiguration(Path: sourcePath, Name: Path.GetFileNameWithoutExtension(sourcePath));
                sources.Add((sourceConfig, doc));
            }

            if (settings.Verbose)
                AnsiConsole.MarkupLine("[grey]Merging...[/]");

            var result = _merger.Merge(config, sources);

            if (result.Errors.Count > 0)
            {
                foreach (var err in result.Errors)
                    AnsiConsole.MarkupLine("[red]ERROR: " + err.Message + "[/]");
                return 1;
            }

            if (settings.Verbose || result.Warnings.Count > 0)
            {
                foreach (var warning in result.Warnings)
                    AnsiConsole.MarkupLine("[yellow]WARN: " + warning.Message + "[/]");
            }

            var outputPath = settings.Output ?? config.Output;
            var format = ParseOutputFormat(settings.Format);

            await _serializer.SerializeToFileAsync(result.Document, outputPath, format);

            var warningCount = result.Warnings.Count;
            var summary = "[green]Merged " + sourcePaths.Count + " file(s) into '" + outputPath + "'.[/]";
            if (warningCount > 0)
                summary += " [yellow]" + warningCount + " warning(s).[/]";

            AnsiConsole.MarkupLine(summary);
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    private static (MergeConfiguration Config, IReadOnlyList<string> SourcePaths) LoadFromConfig(string configPath)
    {
        var config = MergeConfigurationLoader.Load(configPath);
        var paths = config.Sources.Select(s => s.Path).ToList();
        return (config, paths);
    }

    private static (MergeConfiguration Config, IReadOnlyList<string> SourcePaths) BuildFromArgs(Settings settings)
    {
        if (settings.Files is null || settings.Files.Length == 0)
            throw new InvalidOperationException("No source files specified. Use --config or provide file paths.");

        if (string.IsNullOrWhiteSpace(settings.Title))
            throw new InvalidOperationException("--title is required when not using --config.");
        if (string.IsNullOrWhiteSpace(settings.Version))
            throw new InvalidOperationException("--version is required when not using --config.");

        var config = new MergeConfiguration
        {
            Info = new MergeInfoConfiguration(settings.Title!, settings.Version!),
            Output = settings.Output ?? "merged-openapi.json",
            Sources = settings.Files.Select(f => new SourceConfiguration(
                Path: f,
                Name: Path.GetFileNameWithoutExtension(f))).ToList(),
        };

        config.Conflicts.Schemas = new MergeComponentConflictPolicy(
            MergeConfigurationLoader.ParseDuplicateHandling(settings.SchemaIdentical, config.Conflicts.Schemas.Identical, "--schema-identical"),
            MergeConfigurationLoader.ParseConflictResolution(settings.SchemaConflict, config.Conflicts.Schemas.Conflict, "--schema-conflict"));

        return (config, settings.Files.ToList());
    }

    private static string ParseOutputFormat(string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
            return "json";

        return format.ToLowerInvariant() switch
        {
            "json" => "json",
            "yaml" or "yml" => "yaml",
            _ => throw new InvalidOperationException("Invalid value '" + format + "' for --format. Allowed values: json, yaml."),
        };
    }
}
