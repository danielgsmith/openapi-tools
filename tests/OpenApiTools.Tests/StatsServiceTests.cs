using OpenApiTools.Services;
using Xunit;

namespace OpenApiTools.Tests;

public class StatsServiceTests
{
    private static string SamplePath => Path.Combine(AppContext.BaseDirectory, "samples", "store-api-v2.yaml");

    [Fact]
    public async Task Compute_Should_Count_Paths_And_Operations()
    {
        var loader = new OpenApiLoader();
        var doc = await loader.LoadAsync(SamplePath);
        var stats = new OpenApiStatsService().Compute(doc);

        Assert.True(stats.PathCount >= 5);
        Assert.True(stats.OperationCount >= 7);
    }

    [Fact]
    public async Task Compute_Should_Count_Deprecated_Operations()
    {
        var loader = new OpenApiLoader();
        var doc = await loader.LoadAsync(SamplePath);
        var stats = new OpenApiStatsService().Compute(doc);

        Assert.True(stats.DeprecatedOperations >= 1, "Should find at least 1 deprecated operation");
    }

    [Fact]
    public async Task Compute_Should_Count_Schemas()
    {
        var loader = new OpenApiLoader();
        var doc = await loader.LoadAsync(SamplePath);
        var stats = new OpenApiStatsService().Compute(doc);

        Assert.True(stats.SchemaCount >= 5);
    }

    [Fact]
    public async Task Compute_Should_Detect_Ops_Without_Description()
    {
        var loader = new OpenApiLoader();
        var doc = await loader.LoadAsync(SamplePath);
        var stats = new OpenApiStatsService().Compute(doc);

        Assert.True(stats.OperationsWithoutDescription >= 1, "Should find ops without descriptions");
    }

    [Fact]
    public async Task Compute_Should_Have_MethodsBreakdown()
    {
        var loader = new OpenApiLoader();
        var doc = await loader.LoadAsync(SamplePath);
        var stats = new OpenApiStatsService().Compute(doc);

        Assert.True(stats.MethodsBreakdown.ContainsKey("GET"));
        Assert.True(stats.MethodsBreakdown["GET"] >= 5);
    }
}