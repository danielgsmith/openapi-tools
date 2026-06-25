using OpenApiTools.Services;
using Xunit;

namespace OpenApiTools.Tests;

public class SplitterTests
{
    private static string SamplePath => Path.Combine(AppContext.BaseDirectory, "samples", "store-api-v2.yaml");

    [Fact]
    public async Task SplitAsync_Should_Create_Output_Directory()
    {
        var loader = new OpenApiLoader();
        var doc = await loader.LoadAsync(SamplePath);
        var splitter = new OpenApiSplitter();
        var outputDir = Path.Combine(Path.GetTempPath(), "openapi-split-test-" + Guid.NewGuid().ToString("N")[..8]);

        try
        {
            await splitter.SplitAsync(doc, outputDir);

            Assert.True(Directory.Exists(outputDir));
            Assert.True(File.Exists(Path.Combine(outputDir, "openapi.yaml")));
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task SplitAsync_Should_Write_Component_Files()
    {
        var loader = new OpenApiLoader();
        var doc = await loader.LoadAsync(SamplePath);
        var splitter = new OpenApiSplitter();
        var outputDir = Path.Combine(Path.GetTempPath(), "openapi-split-test-" + Guid.NewGuid().ToString("N")[..8]);

        try
        {
            await splitter.SplitAsync(doc, outputDir);

            var schemasDir = Path.Combine(outputDir, "components", "schemas");
            Assert.True(Directory.Exists(schemasDir));
            Assert.True(File.Exists(Path.Combine(schemasDir, "Product.yaml")));
            Assert.True(File.Exists(Path.Combine(schemasDir, "Order.yaml")));
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task SplitAsync_Should_Write_Parameter_Files()
    {
        var loader = new OpenApiLoader();
        var doc = await loader.LoadAsync(SamplePath);
        var splitter = new OpenApiSplitter();
        var outputDir = Path.Combine(Path.GetTempPath(), "openapi-split-test-" + Guid.NewGuid().ToString("N")[..8]);

        try
        {
            await splitter.SplitAsync(doc, outputDir);

            var paramsDir = Path.Combine(outputDir, "components", "parameters");
            Assert.True(Directory.Exists(paramsDir));
            Assert.True(File.Exists(Path.Combine(paramsDir, "OrderIdParam.yaml")));
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task SplitAsync_Should_Write_SecurityScheme_Files()
    {
        var loader = new OpenApiLoader();
        var doc = await loader.LoadAsync(SamplePath);
        var splitter = new OpenApiSplitter();
        var outputDir = Path.Combine(Path.GetTempPath(), "openapi-split-test-" + Guid.NewGuid().ToString("N")[..8]);

        try
        {
            await splitter.SplitAsync(doc, outputDir);

            var secDir = Path.Combine(outputDir, "components", "securitySchemes");
            Assert.True(Directory.Exists(secDir));
            Assert.True(File.Exists(Path.Combine(secDir, "ApiKey.yaml")));
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, recursive: true);
        }
    }
}