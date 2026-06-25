using OpenApiTools.Services;
using Xunit;

namespace OpenApiTools.Tests;

public class ResolverTests
{
    private static string SamplePath => Path.Combine(AppContext.BaseDirectory, "samples", "store-api-v2.yaml");

    [Fact]
    public async Task ResolveAndSerialize_Should_Produce_Yaml_With_InlinedRefs()
    {
        var loader = new OpenApiLoader();
        var doc = await loader.LoadAsync(SamplePath);
        var resolver = new OpenApiResolver();

        var result = resolver.ResolveAndSerialize(doc, "yaml");

        Assert.False(string.IsNullOrEmpty(result));
        Assert.DoesNotContain("$ref: '#/components/schemas/Product'", result);
        Assert.Contains("openapi:", result);
    }

    [Fact]
    public async Task ResolveAndSerialize_Should_Produce_Json()
    {
        var loader = new OpenApiLoader();
        var doc = await loader.LoadAsync(SamplePath);
        var resolver = new OpenApiResolver();

        var result = resolver.ResolveAndSerialize(doc, "json");

        Assert.False(string.IsNullOrEmpty(result));
        Assert.Contains("\"openapi\"", result);
    }

    [Fact]
    public async Task ResolveAndSerialize_Should_Be_Valid_OpenApi()
    {
        var loader = new OpenApiLoader();
        var doc = await loader.LoadAsync(SamplePath);
        var resolver = new OpenApiResolver();

        var resolved = resolver.ResolveAndSerialize(doc, "yaml");

        var reader = new Microsoft.OpenApi.Readers.OpenApiStringReader();
        var reparsed = reader.Read(resolved, out var diagnostic);

        Assert.NotNull(reparsed);
    }
}