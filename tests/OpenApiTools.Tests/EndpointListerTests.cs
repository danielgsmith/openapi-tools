using OpenApiTools.Services;
using Xunit;

namespace OpenApiTools.Tests;

public class EndpointListerTests
{
    private static string SamplePath => Path.Combine(AppContext.BaseDirectory, "samples", "store-api-v2.yaml");

    [Fact]
    public async Task List_Should_ReturnAllOperations()
    {
        var loader = new OpenApiLoader();
        var doc = await loader.LoadAsync(SamplePath);
        var lister = new OpenApiEndpointLister();
        var endpoints = lister.List(doc);

        Assert.True(endpoints.Count >= 7);

        var getProducts = endpoints.FirstOrDefault(e => e.Path == "/products" && e.Method == "GET");
        Assert.NotNull(getProducts);
        Assert.Equal("listProducts", getProducts!.OperationId);
        Assert.Contains("products", getProducts.Tags);
    }

    [Fact]
    public async Task List_Should_FlagDeprecated()
    {
        var loader = new OpenApiLoader();
        var doc = await loader.LoadAsync(SamplePath);
        var lister = new OpenApiEndpointLister();
        var endpoints = lister.List(doc);

        var deleteProduct = endpoints.FirstOrDefault(e => e.Path == "/products/{productId}" && e.Method == "DELETE");
        Assert.NotNull(deleteProduct);
        Assert.True(deleteProduct!.Deprecated);
    }
}