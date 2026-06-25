using OpenApiTools.Services;
using Xunit;

namespace OpenApiTools.Tests;

public class DescriberTests
{
    private static string SamplePath => Path.Combine(AppContext.BaseDirectory, "samples", "store-api-v2.yaml");

    [Fact]
    public async Task DescribeEndpoint_Should_ReturnFullDetail()
    {
        var loader = new OpenApiLoader();
        var doc = await loader.LoadAsync(SamplePath);
        var describer = new OpenApiDescriber();

        var detail = describer.DescribeEndpoint(doc, "GET /products");

        Assert.Equal("/products", detail.Path);
        Assert.Equal("GET", detail.Method);
        Assert.Equal("listProducts", detail.OperationId);
        Assert.True(detail.Parameters.Count >= 1);
        Assert.True(detail.Responses.Count >= 2);
    }

    [Fact]
    public async Task DescribeEndpoint_Should_FlagDeprecated()
    {
        var loader = new OpenApiLoader();
        var doc = await loader.LoadAsync(SamplePath);
        var describer = new OpenApiDescriber();

        var detail = describer.DescribeEndpoint(doc, "DELETE /products/{productId}");

        Assert.True(detail.Deprecated);
    }

    [Fact]
    public async Task DescribeEndpoint_Should_IncludeRequestBody()
    {
        var loader = new OpenApiLoader();
        var doc = await loader.LoadAsync(SamplePath);
        var describer = new OpenApiDescriber();

        var detail = describer.DescribeEndpoint(doc, "POST /products");

        Assert.NotNull(detail.RequestBody);
        Assert.True(detail.RequestBody!.Required);
    }

    [Fact]
    public async Task DescribeSchema_Should_ReturnProperties()
    {
        var loader = new OpenApiLoader();
        var doc = await loader.LoadAsync(SamplePath);
        var describer = new OpenApiDescriber();

        var detail = describer.DescribeSchema(doc, "Product");

        Assert.Equal("Product", detail.Name);
        Assert.Equal("object", detail.Type);
        Assert.True(detail.Properties.Count >= 3);
        Assert.Contains(detail.Properties, p => p.Name == "id");
        Assert.Contains(detail.Properties, p => p.Name == "price" && p.Required);
    }

    [Fact]
    public async Task DescribeSchema_Should_Throw_ForNotFound()
    {
        var loader = new OpenApiLoader();
        var doc = await loader.LoadAsync(SamplePath);
        var describer = new OpenApiDescriber();

        Assert.Throws<KeyNotFoundException>(() => describer.DescribeSchema(doc, "NonExistent"));
    }
}