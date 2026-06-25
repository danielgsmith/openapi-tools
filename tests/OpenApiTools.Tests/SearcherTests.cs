using OpenApiTools.Services;
using Xunit;

namespace OpenApiTools.Tests;

public class SearcherTests
{
    private static string SamplePath => Path.Combine(AppContext.BaseDirectory, "samples", "store-api-v2.yaml");

    [Fact]
    public async Task Search_Should_Find_Endpoints()
    {
        var loader = new OpenApiLoader();
        var doc = await loader.LoadAsync(SamplePath);
        var searcher = new OpenApiSearcher();
        var results = searcher.Search(doc, "product", new HashSet<string> { "endpoints" });

        Assert.True(results.Count >= 3);
        Assert.All(results, r => Assert.Equal("endpoint", r.Component));
    }

    [Fact]
    public async Task Search_Should_Find_Schemas()
    {
        var loader = new OpenApiLoader();
        var doc = await loader.LoadAsync(SamplePath);
        var searcher = new OpenApiSearcher();
        var results = searcher.Search(doc, "product", new HashSet<string> { "schemas" });

        Assert.True(results.Count >= 1);
        Assert.Contains(results, r => r.Name == "Product");
    }

    [Fact]
    public async Task Search_Should_Find_Across_Components()
    {
        var loader = new OpenApiLoader();
        var doc = await loader.LoadAsync(SamplePath);
        var searcher = new OpenApiSearcher();
        var results = searcher.Search(doc, "order", new HashSet<string> { "endpoints", "schemas" });

        Assert.True(results.Count >= 2);
        Assert.Contains(results, r => r.Component == "endpoint");
        Assert.Contains(results, r => r.Component == "schema");
    }

    [Fact]
    public async Task Search_Should_ReturnEmpty_For_NoMatches()
    {
        var loader = new OpenApiLoader();
        var doc = await loader.LoadAsync(SamplePath);
        var searcher = new OpenApiSearcher();
        var results = searcher.Search(doc, "zzzznotfound", new HashSet<string> { "endpoints", "schemas" });

        Assert.Empty(results);
    }
}