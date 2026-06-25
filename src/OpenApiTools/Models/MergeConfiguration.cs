using Microsoft.OpenApi.Models;

namespace OpenApiTools.Models;

public enum SchemaConflictStrategy
{
    Rename,
    FirstWins,
    Fail,
}

public sealed record MergeInfoConfiguration(
    string Title,
    string Version,
    string? Description = null);

public sealed record MergeServerConfiguration(
    string Url,
    string? Description = null);

public sealed record SourceConfiguration(
    string Path,
    string? PathPrefix = null,
    string? OperationIdPrefix = null,
    string? Name = null);

public sealed class MergeConfiguration
{
    public MergeInfoConfiguration Info { get; set; } = new("", "");
    public List<MergeServerConfiguration> Servers { get; set; } = new();
    public List<SourceConfiguration> Sources { get; set; } = new();
    public string Output { get; set; } = "merged-openapi.json";
    public SchemaConflictStrategy SchemaConflict { get; set; } = SchemaConflictStrategy.Rename;
}

public sealed record MergeWarning(string Message, string? Source = null);

public sealed class MergeResult
{
    public required OpenApiDocument Document { get; init; }
    public required IReadOnlyList<MergeWarning> Warnings { get; init; }
    public bool Success => !Warnings.Any(w => w.Message.StartsWith("ERROR", StringComparison.Ordinal));
}

public static class MergeConfigurationLoader
{
    public static MergeConfiguration Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Merge config not found: " + path, path);

        var json = File.ReadAllText(path);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;

        var infoElement = root.TryGetProperty("info", out var info) ? info : default;
        var title = infoElement.ValueKind != System.Text.Json.JsonValueKind.Undefined && infoElement.TryGetProperty("title", out var titleProp)
            ? titleProp.GetString() ?? string.Empty
            : string.Empty;
        var version = infoElement.ValueKind != System.Text.Json.JsonValueKind.Undefined && infoElement.TryGetProperty("version", out var versionProp)
            ? versionProp.GetString() ?? string.Empty
            : string.Empty;
        var description = infoElement.ValueKind != System.Text.Json.JsonValueKind.Undefined && infoElement.TryGetProperty("description", out var descriptionProp)
            ? descriptionProp.GetString()
            : null;

        var config = new MergeConfiguration
        {
            Info = new MergeInfoConfiguration(title, version, description),
            Output = root.TryGetProperty("output", out var outputProp)
                ? outputProp.GetString() ?? "merged-openapi.json"
                : "merged-openapi.json",
            SchemaConflict = root.TryGetProperty("schemaConflict", out var schemaConflictElement)
                && schemaConflictElement.ValueKind == System.Text.Json.JsonValueKind.String
                ? schemaConflictElement.GetString()?.ToLowerInvariant() switch
                {
                    "first-wins" or "firstwins" => SchemaConflictStrategy.FirstWins,
                    "fail" => SchemaConflictStrategy.Fail,
                    _ => SchemaConflictStrategy.Rename,
                }
                : SchemaConflictStrategy.Rename,
        };

        if (root.TryGetProperty("servers", out var serversElement) && serversElement.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var serverElement in serversElement.EnumerateArray())
            {
                config.Servers.Add(new MergeServerConfiguration(
                    serverElement.TryGetProperty("url", out var urlProp) ? urlProp.GetString() ?? string.Empty : string.Empty,
                    serverElement.TryGetProperty("description", out var serverDescProp) ? serverDescProp.GetString() : null));
            }
        }

        if (root.TryGetProperty("sources", out var sourcesElement) && sourcesElement.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var sourceElement in sourcesElement.EnumerateArray())
            {
                config.Sources.Add(new SourceConfiguration(
                    Path: sourceElement.TryGetProperty("path", out var pathProp) ? pathProp.GetString() ?? string.Empty : string.Empty,
                    PathPrefix: sourceElement.TryGetProperty("pathPrefix", out var pathPrefixProp) ? pathPrefixProp.GetString() : null,
                    OperationIdPrefix: sourceElement.TryGetProperty("operationIdPrefix", out var opPrefixProp) ? opPrefixProp.GetString() : null,
                    Name: sourceElement.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null));
            }
        }

        if (config.Sources.Count == 0)
            throw new InvalidOperationException("Merge config must contain at least one source.");

        if (string.IsNullOrWhiteSpace(config.Info.Title))
            throw new InvalidOperationException("Merge config must contain an info.title.");
        if (string.IsNullOrWhiteSpace(config.Info.Version))
            throw new InvalidOperationException("Merge config must contain an info.version.");

        return config;
    }
}
