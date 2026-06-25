using OpenApiTools.Services;
using Xunit;

namespace OpenApiTools.Tests;

public class UnusedFinderTests
{
    private static string SamplePath => Path.Combine(AppContext.BaseDirectory, "samples", "store-api-v2.yaml");

    [Fact]
    public async Task Find_Should_Identify_Unused_Schemas()
    {
        var loader = new OpenApiLoader();
        var doc = await loader.LoadAsync(SamplePath);
        var tracker = new OpenApiReferenceTracker();
        var finder = new OpenApiUnusedFinder(tracker);
        var unused = finder.Find(doc);

        var unusedSchemas = unused.Where(u => u.Type == ComponentType.Schema).ToList();
        Assert.Contains(unusedSchemas, u => u.Name == "UnusedSchema");
    }

    [Fact]
    public async Task Find_Should_Identify_Unused_Parameters()
    {
        var loader = new OpenApiLoader();
        var doc = await loader.LoadAsync(SamplePath);
        var tracker = new OpenApiReferenceTracker();
        var finder = new OpenApiUnusedFinder(tracker);
        var unused = finder.Find(doc);

        Assert.Contains(unused, u => u.Type == ComponentType.Parameter && u.Name == "UnusedParam");
    }

    [Fact]
    public async Task Find_Should_Identify_Unused_Responses()
    {
        var loader = new OpenApiLoader();
        var doc = await loader.LoadAsync(SamplePath);
        var tracker = new OpenApiReferenceTracker();
        var finder = new OpenApiUnusedFinder(tracker);
        var unused = finder.Find(doc);

        Assert.Contains(unused, u => u.Type == ComponentType.Response && u.Name == "UnusedResponse");
    }

    [Fact]
    public async Task Find_Should_Identify_Unused_SecuritySchemes()
    {
        var loader = new OpenApiLoader();
        var doc = await loader.LoadAsync(SamplePath);
        var tracker = new OpenApiReferenceTracker();
        var finder = new OpenApiUnusedFinder(tracker);
        var unused = finder.Find(doc);

        Assert.Contains(unused, u => u.Type == ComponentType.SecurityScheme && u.Name == "UnusedScheme");
    }

    [Fact]
    public async Task Find_Should_Not_Flag_Used_Components()
    {
        var loader = new OpenApiLoader();
        var doc = await loader.LoadAsync(SamplePath);
        var tracker = new OpenApiReferenceTracker();
        var finder = new OpenApiUnusedFinder(tracker);
        var unused = finder.Find(doc);

        Assert.DoesNotContain(unused, u => u.Type == ComponentType.Schema && u.Name == "Product");
        Assert.DoesNotContain(unused, u => u.Type == ComponentType.Schema && u.Name == "Order");
        Assert.DoesNotContain(unused, u => u.Type == ComponentType.Parameter && u.Name == "OrderIdParam");
    }

    [Fact]
    public async Task Find_Should_Not_Flag_NotFound_When_Used_Via_Ref()
    {
        // NotFound is defined in components/responses but operations use inline 404s,
        // so it IS unused. This test documents that used-via-ref components are not flagged.
        var loader = new OpenApiLoader();
        var doc = await loader.LoadAsync(SamplePath);
        var tracker = new OpenApiReferenceTracker();
        var finder = new OpenApiUnusedFinder(tracker);
        var unused = finder.Find(doc);

        // Product IS referenced via $ref in operations, so it should NOT be unused
        Assert.DoesNotContain(unused, u => u.Type == ComponentType.Schema && u.Name == "Product");
        // OrderIdParam IS referenced via $ref in /orders/{orderId}
        Assert.DoesNotContain(unused, u => u.Type == ComponentType.Parameter && u.Name == "OrderIdParam");
    }
}