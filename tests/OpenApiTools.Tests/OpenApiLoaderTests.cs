using OpenApiTools.Services;
using Xunit;

namespace OpenApiTools.Tests;

public class OpenApiLoaderTests
{
    [Fact]
    public async Task LoadAsync_Should_Read_Valid_Petstore_Spec()
    {
        var loader = new OpenApiLoader();
        var path = Path.Combine(AppContext.BaseDirectory, "samples", "petstore.yaml");

        var document = await loader.LoadAsync(path);

        Assert.NotNull(document);
        Assert.Equal("Swagger Petstore", document.Info.Title);
    }
}