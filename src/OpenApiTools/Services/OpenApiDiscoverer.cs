using System.Text.RegularExpressions;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using OpenApiTools.Models;

namespace OpenApiTools.Services;

public sealed class OpenApiDiscoverer : IOpenApiDiscoverer
{
    // Match a top-level OpenAPI/Swagger spec-version key, in either YAML or JSON form:
    //   openapi: 3.0.3
    //   "openapi": "3.0.3"
    //   swagger: "2.0"
    // Anchored to line start so references inside prose descriptions don't match.
    private static readonly Regex SpecMarker = new(
        @"(?m)^\s*[""']?(openapi|swagger)[""']?\s*:",
        RegexOptions.Compiled);

    private const int PeekBytes = 4 * 1024;

    public async Task<IReadOnlyList<DiscoveryResult>> DiscoverAsync(
        string root,
        DiscoverOptions options,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Directory not found: {root}");
        }

        var results = new List<DiscoveryResult>();

        var enumeration = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.Device | FileAttributes.ReparsePoint,
        };

        foreach (var file in Directory.EnumerateFiles(root, "*", enumeration))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var ext = Path.GetExtension(file);
            if (!options.Extensions.Contains(ext))
            {
                continue;
            }

            var info = new FileInfo(file);
            if (info.Length == 0 || info.Length > options.MaxSizeBytes)
            {
                continue;
            }

            if (!HasOpenApiMarker(file))
            {
                continue;
            }

            if (!options.Validate)
            {
                results.Add(new DiscoveryResult(
                    Path: file,
                    SpecVersion: null,
                    Title: null,
                    DocumentVersion: null,
                    Status: DiscoveryStatus.Valid,
                    Error: null));
                continue;
            }

            var parsed = await TryParseAsync(file, cancellationToken);
            if (parsed.Document is null)
            {
                results.Add(new DiscoveryResult(
                    Path: file,
                    SpecVersion: null,
                    Title: null,
                    DocumentVersion: null,
                    Status: DiscoveryStatus.Invalid,
                    Error: parsed.Error));
                continue;
            }

            results.Add(new DiscoveryResult(
                Path: file,
                SpecVersion: parsed.SpecVersion,
                Title: parsed.Document.Info?.Title,
                DocumentVersion: parsed.Document.Info?.Version,
                Status: DiscoveryStatus.Valid));
        }

        return results;
    }

    private static bool HasOpenApiMarker(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var buffer = new byte[Math.Min(PeekBytes, (int)stream.Length)];
            var read = stream.Read(buffer, 0, buffer.Length);
            var text = System.Text.Encoding.UTF8.GetString(buffer, 0, read);
            return SpecMarker.IsMatch(text);
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static async Task<(OpenApiDocument? Document, string? SpecVersion, string? Error)> TryParseAsync(
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(path);
            var reader = new OpenApiStreamReader();
            var result = await reader.ReadAsync(stream, cancellationToken);
            if (result.OpenApiDocument is null)
            {
                var errors = string.Join("; ", result.OpenApiDiagnostic?.Errors ?? []);
                return (null, null, errors.Length == 0 ? "Unknown parse error" : errors);
            }

            var specVersion = result.OpenApiDiagnostic?.SpecificationVersion.ToString();
            return (result.OpenApiDocument, specVersion, null);
        }
        catch (Exception ex)
        {
            return (null, null, ex.Message);
        }
    }
}