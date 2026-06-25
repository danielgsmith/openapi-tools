using OpenApiTools.Models;
using OpenApiTools.Services;
using Xunit;

namespace OpenApiTools.Tests;

public class OpenApiDiscovererTests
{
    private static string SamplesDir =>
        Path.Combine(AppContext.BaseDirectory, "samples");

    private static async Task<IReadOnlyList<DiscoveryResult>> DiscoverAsync(DiscoverOptions? options = null)
    {
        var discoverer = new OpenApiDiscoverer();
        return await discoverer.DiscoverAsync(SamplesDir, options ?? DiscoverOptions.Default);
    }

    [Fact]
    public async Task Discover_Should_Find_Petstore_And_Swagger2()
    {
        var results = await DiscoverAsync();

        var names = results.Select(r => Path.GetFileName(r.Path)).ToHashSet();
        Assert.Contains("petstore.yaml", names);
        Assert.Contains("swagger2.yaml", names);
        Assert.DoesNotContain("not-openapi.json", names);
    }

    [Fact]
    public async Task Discover_Should_Mark_All_Found_As_Valid()
    {
        var results = await DiscoverAsync();
        Assert.All(results, r => Assert.Equal(DiscoveryStatus.Valid, r.Status));
    }

    [Fact]
    public async Task Discover_Should_Extract_Title_And_Version()
    {
        var results = await DiscoverAsync();
        var petstore = results.Single(r => r.Path.EndsWith("petstore.yaml"));
        Assert.Equal("Swagger Petstore", petstore.Title);
        Assert.Equal("1.0.0", petstore.DocumentVersion);
    }

    [Fact]
    public async Task Discover_NoValidate_Should_Skip_Parse()
    {
        var options = DiscoverOptions.Default with { Validate = false };
        var results = await DiscoverAsync(options);
        Assert.All(results, r =>
        {
            Assert.Null(r.SpecVersion);
            Assert.Null(r.Title);
            Assert.Null(r.DocumentVersion);
        });
    }
}