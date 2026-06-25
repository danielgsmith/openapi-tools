namespace OpenApiTools.Models;

public sealed record SearchResult(
    string Component,
    string Name,
    string Path,
    string? Description,
    double Score);