namespace OpenApiTools.Models;

public enum DiscoveryStatus
{
    Valid,
    Invalid,
    Skipped,
}

public sealed record DiscoveryResult(
    string Path,
    string? SpecVersion,
    string? Title,
    string? DocumentVersion,
    DiscoveryStatus Status,
    string? Error = null);