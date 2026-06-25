using Microsoft.OpenApi.Models;
using OpenApiTools.Models;
using OpenApiTools.Services;
using Xunit;

namespace OpenApiTools.Tests;

public class MergerTests
{
    private static string UsersPath => Path.Combine(AppContext.BaseDirectory, "samples", "users-api.yaml");
    private static string ProductsPath => Path.Combine(AppContext.BaseDirectory, "samples", "products-api.yaml");

    private static async Task<(MergeResult Result, OpenApiMerger Merger)> MergeAsync(
        SchemaConflictStrategy strategy = SchemaConflictStrategy.Rename,
        string? usersPathPrefix = null,
        string? productsPathPrefix = null,
        string? usersOpIdPrefix = null,
        string? productsOpIdPrefix = null)
    {
        var loader = new OpenApiLoader();
        var usersDoc = await loader.LoadAsync(UsersPath);
        var productsDoc = await loader.LoadAsync(ProductsPath);

        var config = new MergeConfiguration
        {
            Info = new MergeInfoConfiguration("Platform API", "1.0.0", "Merged API"),
            SchemaConflict = strategy,
            Sources =
            [
                new SourceConfiguration(UsersPath, usersPathPrefix, usersOpIdPrefix, "Users"),
                new SourceConfiguration(ProductsPath, productsPathPrefix, productsOpIdPrefix, "Products"),
            ],
        };

        var merger = new OpenApiMerger();
        var result = merger.Merge(config, new List<(SourceConfiguration, OpenApiDocument)>
        {
            (config.Sources[0], usersDoc),
            (config.Sources[1], productsDoc),
        });

        return (result, merger);
    }

    [Fact]
    public async Task Merge_Should_Combine_Paths_From_All_Sources()
    {
        var (result, _) = await MergeAsync();

        Assert.True(result.Document.Paths.Count >= 4);
        Assert.Contains("/users", result.Document.Paths.Keys);
        Assert.Contains("/products", result.Document.Paths.Keys);
    }

    [Fact]
    public async Task Merge_Should_Apply_Path_Prefixes()
    {
        var (result, _) = await MergeAsync(
            usersPathPrefix: "/users",
            productsPathPrefix: "/products");

        Assert.Contains("/users/users", result.Document.Paths.Keys);
        Assert.Contains("/products/products", result.Document.Paths.Keys);
    }

    [Fact]
    public async Task Merge_Should_Apply_OperationId_Prefixes()
    {
        var (result, _) = await MergeAsync(
            usersOpIdPrefix: "users_",
            productsOpIdPrefix: "products_");

        var allOpIds = result.Document.Paths
            .SelectMany(p => p.Value.Operations.Values)
            .Select(o => o.OperationId)
            .Where(id => id is not null)
            .Select(id => id!)
            .ToHashSet();

        Assert.Contains("users_listUsers", allOpIds);
        Assert.Contains("products_listProducts", allOpIds);
    }

    [Fact]
    public async Task Merge_Should_Use_Configured_Title_And_Version()
    {
        var (result, _) = await MergeAsync();

        Assert.Equal("Platform API", result.Document.Info.Title);
        Assert.Equal("1.0.0", result.Document.Info.Version);
        Assert.Equal("Merged API", result.Document.Info.Description);
    }

    [Fact]
    public async Task Merge_Should_Use_Configured_Servers_Not_Source_Servers()
    {
        var config = new MergeConfiguration
        {
            Info = new MergeInfoConfiguration("Platform API", "1.0.0"),
            Servers = [new MergeServerConfiguration("https://api.example.com", "Production")],
            SchemaConflict = SchemaConflictStrategy.Rename,
            Sources = [new SourceConfiguration(UsersPath, Name: "Users")],
        };

        var loader = new OpenApiLoader();
        var doc = await loader.LoadAsync(UsersPath);
        var merger = new OpenApiMerger();
        var result = merger.Merge(config, new List<(SourceConfiguration, OpenApiDocument)>
        {
            (config.Sources[0], doc),
        });

        Assert.Single(result.Document.Servers);
        Assert.Equal("https://api.example.com", result.Document.Servers[0].Url);
    }

    [Fact]
    public async Task Merge_Rename_Should_Rename_Conflicting_Schemas()
    {
        var (result, _) = await MergeAsync(strategy: SchemaConflictStrategy.Rename);

        var schemaNames = result.Document.Components?.Schemas?.Keys ?? [];
        Assert.Contains("Error", schemaNames);
        Assert.Contains("Products_Error", schemaNames);
    }

    [Fact]
    public async Task Merge_FirstWins_Should_Keep_First_Schema_And_Warn()
    {
        var (result, _) = await MergeAsync(strategy: SchemaConflictStrategy.FirstWins);

        var schemaNames = result.Document.Components?.Schemas?.Keys ?? [];
        Assert.Contains("Error", schemaNames);
        Assert.DoesNotContain("Products_Error", schemaNames);

        Assert.Contains(result.Warnings, w => w.Message.Contains("Error") && w.Message.Contains("first-wins"));
    }

    [Fact]
    public async Task Merge_Fail_Should_Generate_Error_Warning()
    {
        var (result, _) = await MergeAsync(strategy: SchemaConflictStrategy.Fail);

        Assert.Contains(result.Warnings, w => w.Message.StartsWith("ERROR") && w.Message.Contains("Error"));
    }

    [Fact]
    public async Task Merge_Should_Combine_SecuritySchemes_Without_Duplicates()
    {
        var (result, _) = await MergeAsync();

        var schemes = result.Document.Components?.SecuritySchemes;
        Assert.NotNull(schemes);
        Assert.Contains("ApiKey", schemes!.Keys);
        Assert.Single(schemes.Keys);
    }

    [Fact]
    public async Task Merge_Should_Warn_On_Duplicate_SecuritySchemes()
    {
        var (result, _) = await MergeAsync();

        Assert.Contains(result.Warnings, w => w.Message.Contains("ApiKey") && w.Message.Contains("already exists"));
    }

    [Fact]
    public async Task Merge_Should_Preserve_Operation_Tags()
    {
        var (result, _) = await MergeAsync();

        var tagNames = result.Document.Paths
            .SelectMany(p => p.Value.Operations.Values)
            .SelectMany(o => o.Tags ?? [])
            .Select(t => t.Name ?? "")
            .Where(n => !string.IsNullOrEmpty(n))
            .ToHashSet();

        Assert.Contains("users", tagNames);
        Assert.Contains("products", tagNames);
    }

    [Fact]
    public async Task Merge_Should_Not_Duplicate_Identical_Paths()
    {
        var (result, _) = await MergeAsync();

        Assert.True(result.Document.Paths.Count == 4);
    }

    [Fact]
    public async Task Merge_Rename_Should_Preserve_Unique_Schemas()
    {
        var (result, _) = await MergeAsync(strategy: SchemaConflictStrategy.Rename);

        var schemaNames = result.Document.Components?.Schemas?.Keys ?? [];
        Assert.Contains("User", schemaNames);
        Assert.Contains("Product", schemaNames);
    }

    [Fact]
    public async Task Merge_Should_Preserve_Schema_Properties()
    {
        var (result, _) = await MergeAsync();

        var product = result.Document.Components?.Schemas?.TryGetValue("Product", out var p) == true ? p : null;
        Assert.NotNull(product);
        Assert.True(product!.Properties!.Count >= 3);
        Assert.Contains("price", product.Properties!.Keys);
    }
}