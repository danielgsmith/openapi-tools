using Microsoft.OpenApi.Models;

namespace OpenApiTools.Models;

public enum MergeDuplicateHandling
{
    Dedupe,
    WarnAndDedupe,
    Fail,
}

public enum MergeConflictResolution
{
    KeepExisting,
    KeepIncoming,
    RenameExisting,
    RenameIncoming,
    RenameBoth,
    Fail,
}

public sealed record MergeComponentConflictPolicy(
    MergeDuplicateHandling Identical = MergeDuplicateHandling.Dedupe,
    MergeConflictResolution Conflict = MergeConflictResolution.KeepExisting);

public sealed class MergeConflictPolicies
{
    public MergeComponentConflictPolicy Schemas { get; set; } = new(MergeDuplicateHandling.Dedupe, MergeConflictResolution.RenameIncoming);
    public MergeComponentConflictPolicy Parameters { get; set; } = new();
    public MergeComponentConflictPolicy Responses { get; set; } = new();
    public MergeComponentConflictPolicy RequestBodies { get; set; } = new();
    public MergeComponentConflictPolicy Headers { get; set; } = new();
    public MergeComponentConflictPolicy Examples { get; set; } = new();
    public MergeComponentConflictPolicy Links { get; set; } = new();
    public MergeComponentConflictPolicy Callbacks { get; set; } = new();
    public MergeComponentConflictPolicy SecuritySchemes { get; set; } = new();
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
    public MergeConflictPolicies Conflicts { get; set; } = new();
}

public sealed class MergeResult
{
    public required OpenApiDocument Document { get; init; }
    public required IReadOnlyList<MergeDiagnostic> Diagnostics { get; init; }
    public IReadOnlyList<MergeDiagnostic> Warnings => Diagnostics.Where(d => d.Severity == MergeDiagnosticSeverity.Warning).ToList();
    public IReadOnlyList<MergeDiagnostic> Errors => Diagnostics.Where(d => d.Severity == MergeDiagnosticSeverity.Error).ToList();
    public bool Success => Errors.Count == 0;
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
            Conflicts = LoadConflictPolicies(root),
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

    private static MergeConflictPolicies LoadConflictPolicies(System.Text.Json.JsonElement root)
    {
        var policies = new MergeConflictPolicies();

        if (!root.TryGetProperty("conflicts", out var conflictsElement)
            || conflictsElement.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            return policies;
        }

        ApplyPolicy(conflictsElement, "schemas", policy => policies.Schemas = policy, policies.Schemas);
        ApplyPolicy(conflictsElement, "parameters", policy => policies.Parameters = policy, policies.Parameters);
        ApplyPolicy(conflictsElement, "responses", policy => policies.Responses = policy, policies.Responses);
        ApplyPolicy(conflictsElement, "requestBodies", policy => policies.RequestBodies = policy, policies.RequestBodies);
        ApplyPolicy(conflictsElement, "headers", policy => policies.Headers = policy, policies.Headers);
        ApplyPolicy(conflictsElement, "examples", policy => policies.Examples = policy, policies.Examples);
        ApplyPolicy(conflictsElement, "links", policy => policies.Links = policy, policies.Links);
        ApplyPolicy(conflictsElement, "callbacks", policy => policies.Callbacks = policy, policies.Callbacks);
        ApplyPolicy(conflictsElement, "securitySchemes", policy => policies.SecuritySchemes = policy, policies.SecuritySchemes);

        return policies;
    }

    private static void ApplyPolicy(
        System.Text.Json.JsonElement conflictsElement,
        string propertyName,
        Action<MergeComponentConflictPolicy> assign,
        MergeComponentConflictPolicy fallback)
    {
        if (!conflictsElement.TryGetProperty(propertyName, out var policyElement)
            || policyElement.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            return;
        }

        var identical = policyElement.TryGetProperty("identical", out var identicalElement)
            ? ParseDuplicateHandling(identicalElement.GetString(), fallback.Identical, propertyName + ".identical")
            : fallback.Identical;
        var conflict = policyElement.TryGetProperty("conflict", out var conflictElement)
            ? ParseConflictResolution(conflictElement.GetString(), fallback.Conflict, propertyName + ".conflict")
            : fallback.Conflict;

        assign(new MergeComponentConflictPolicy(identical, conflict));
    }

    public static MergeDuplicateHandling ParseDuplicateHandling(string? value, MergeDuplicateHandling fallback = MergeDuplicateHandling.Dedupe, string? context = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        return value.ToLowerInvariant() switch
        {
            "warn-and-dedupe" => MergeDuplicateHandling.WarnAndDedupe,
            "fail" => MergeDuplicateHandling.Fail,
            "dedupe" => MergeDuplicateHandling.Dedupe,
            _ => throw new InvalidOperationException(BuildInvalidValueMessage(value ?? string.Empty, context, "dedupe, warn-and-dedupe, fail")),
        };
    }

    public static MergeConflictResolution ParseConflictResolution(string? value, MergeConflictResolution fallback = MergeConflictResolution.KeepExisting, string? context = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        return value.ToLowerInvariant() switch
        {
            "keep-incoming" => MergeConflictResolution.KeepIncoming,
            "rename-existing" => MergeConflictResolution.RenameExisting,
            "rename-incoming" => MergeConflictResolution.RenameIncoming,
            "rename-both" => MergeConflictResolution.RenameBoth,
            "fail" => MergeConflictResolution.Fail,
            "keep-existing" => MergeConflictResolution.KeepExisting,
            _ => throw new InvalidOperationException(BuildInvalidValueMessage(value ?? string.Empty, context, "keep-existing, keep-incoming, rename-existing, rename-incoming, rename-both, fail")),
        };
    }

    private static string BuildInvalidValueMessage(string value, string? context, string allowedValues)
    {
        var scope = string.IsNullOrWhiteSpace(context) ? "merge policy" : context;
        return "Invalid value '" + value + "' for " + scope + ". Allowed values: " + allowedValues + ".";
    }
}
