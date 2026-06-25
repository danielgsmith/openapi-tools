using OpenApiTools.Models;

namespace OpenApiTools.Services;

public sealed record DiscoverOptions(
    IReadOnlySet<string> Extensions,
    long MaxSizeBytes,
    bool Validate)
{
    public static DiscoverOptions Default { get; } = new(
        Extensions: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".json", ".yaml", ".yml" },
        MaxSizeBytes: 10L * 1024 * 1024,
        Validate: true);
}

public interface IOpenApiDiscoverer
{
    Task<IReadOnlyList<DiscoveryResult>> DiscoverAsync(
        string root,
        DiscoverOptions options,
        CancellationToken cancellationToken = default);
}