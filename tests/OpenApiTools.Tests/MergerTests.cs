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
        MergeConflictResolution schemaConflict = MergeConflictResolution.RenameIncoming,
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
            Sources =
            [
                new SourceConfiguration(UsersPath, usersPathPrefix, usersOpIdPrefix, "Users"),
                new SourceConfiguration(ProductsPath, productsPathPrefix, productsOpIdPrefix, "Products"),
            ],
        };
        config.Conflicts.Schemas = config.Conflicts.Schemas with { Conflict = schemaConflict };

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
        var (result, _) = await MergeAsync(schemaConflict: MergeConflictResolution.RenameIncoming);

        var schemaNames = result.Document.Components?.Schemas?.Keys ?? [];
        Assert.Contains("Error", schemaNames);
        Assert.Contains("Products_Error", schemaNames);
    }

    [Fact]
    public async Task Merge_KeepExisting_Should_Keep_First_Schema_And_Warn()
    {
        var (result, _) = await MergeAsync(schemaConflict: MergeConflictResolution.KeepExisting);

        var schemaNames = result.Document.Components?.Schemas?.Keys ?? [];
        Assert.Contains("Error", schemaNames);
        Assert.DoesNotContain("Products_Error", schemaNames);

        Assert.Contains(result.Warnings, w => w.Message.Contains("Error") && w.Message.Contains("keeping existing definition"));
    }

    [Fact]
    public async Task Merge_Fail_Should_Generate_Error_Warning()
    {
        var (result, _) = await MergeAsync(schemaConflict: MergeConflictResolution.Fail);

        Assert.Contains(result.Errors, w => w.Message.Contains("Error"));
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
    public async Task Merge_Should_Warn_On_Conflicting_SecuritySchemes()
    {
        var (result, _) = await MergeAsync();

        Assert.Contains(result.Warnings, w => w.Message.Contains("Conflicting security scheme 'ApiKey'"));
    }

    [Fact]
    public void Merge_Should_Deduplicate_Identical_Schemas_Without_Warning()
    {
        var first = new OpenApiDocument
        {
            Components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, OpenApiSchema>
                {
                    ["Error"] = new()
                    {
                        Type = "object",
                        Properties = new Dictionary<string, OpenApiSchema>
                        {
                            ["message"] = new() { Type = "string" },
                        },
                    },
                },
            },
            Paths = new OpenApiPaths(),
        };

        var second = new OpenApiDocument
        {
            Components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, OpenApiSchema>
                {
                    ["Error"] = new()
                    {
                        Properties = new Dictionary<string, OpenApiSchema>
                        {
                            ["message"] = new() { Type = "string" },
                        },
                        Type = "object",
                    },
                },
            },
            Paths = new OpenApiPaths(),
        };

        var config = new MergeConfiguration
        {
            Info = new MergeInfoConfiguration("Platform API", "1.0.0"),
            Sources =
            [
                new SourceConfiguration("first.yaml", Name: "First"),
                new SourceConfiguration("second.yaml", Name: "Second"),
            ],
        };
        config.Conflicts.Schemas = config.Conflicts.Schemas with { Conflict = MergeConflictResolution.RenameIncoming };

        var merger = new OpenApiMerger();
        var result = merger.Merge(config, [(config.Sources[0], first), (config.Sources[1], second)]);

        Assert.Single(result.Document.Components!.Schemas!);
        Assert.Contains("Error", result.Document.Components.Schemas.Keys);
        Assert.DoesNotContain(result.Warnings, w => w.Message.Contains("Error") && w.Message.Contains("renamed"));
    }

    [Fact]
    public void Merge_Should_Rename_Conflicting_Responses_When_Configured()
    {
        var first = new OpenApiDocument
        {
            Components = new OpenApiComponents
            {
                Responses = new Dictionary<string, OpenApiResponse>
                {
                    ["Created"] = new()
                    {
                        Description = "Created A",
                        Content = new Dictionary<string, OpenApiMediaType>
                        {
                            ["application/json"] = new() { Schema = new OpenApiSchema { Type = "string" } },
                        },
                    },
                },
            },
            Paths = new OpenApiPaths
            {
                ["/a"] = new OpenApiPathItem
                {
                    Operations = new Dictionary<OperationType, OpenApiOperation>
                    {
                        [OperationType.Get] = new()
                        {
                            Responses = new OpenApiResponses
                            {
                                ["201"] = new OpenApiResponse
                                {
                                    Reference = new OpenApiReference { Type = ReferenceType.Response, Id = "Created" },
                                    UnresolvedReference = true,
                                },
                            },
                        },
                    },
                },
            },
        };

        var second = new OpenApiDocument
        {
            Components = new OpenApiComponents
            {
                Responses = new Dictionary<string, OpenApiResponse>
                {
                    ["Created"] = new()
                    {
                        Description = "Created B",
                        Content = new Dictionary<string, OpenApiMediaType>
                        {
                            ["application/json"] = new() { Schema = new OpenApiSchema { Type = "integer" } },
                        },
                    },
                },
            },
            Paths = new OpenApiPaths
            {
                ["/b"] = new OpenApiPathItem
                {
                    Operations = new Dictionary<OperationType, OpenApiOperation>
                    {
                        [OperationType.Get] = new()
                        {
                            Responses = new OpenApiResponses
                            {
                                ["201"] = new OpenApiResponse
                                {
                                    Reference = new OpenApiReference { Type = ReferenceType.Response, Id = "Created" },
                                    UnresolvedReference = true,
                                },
                            },
                        },
                    },
                },
            },
        };

        var config = new MergeConfiguration
        {
            Info = new MergeInfoConfiguration("Platform API", "1.0.0"),
            Sources =
            [
                new SourceConfiguration("first.yaml", Name: "First"),
                new SourceConfiguration("second.yaml", Name: "Second"),
            ],
        };
        config.Conflicts.Responses = new MergeComponentConflictPolicy(
            MergeDuplicateHandling.Dedupe,
            MergeConflictResolution.RenameIncoming);

        var merger = new OpenApiMerger();
        var result = merger.Merge(config, [(config.Sources[0], first), (config.Sources[1], second)]);

        Assert.Contains("Created", result.Document.Components!.Responses!.Keys);
        Assert.Contains("Second_Created", result.Document.Components.Responses.Keys);
        Assert.Equal("Created", result.Document.Paths["/a"].Operations[OperationType.Get].Responses["201"].Reference!.Id);
        Assert.Equal("Second_Created", result.Document.Paths["/b"].Operations[OperationType.Get].Responses["201"].Reference!.Id);
    }

    [Fact]
    public void Merge_Should_Replace_Conflicting_Responses_When_KeepIncoming_Is_Configured()
    {
        var first = new OpenApiDocument
        {
            Components = new OpenApiComponents
            {
                Responses = new Dictionary<string, OpenApiResponse>
                {
                    ["Created"] = new() { Description = "Created A" },
                },
            },
            Paths = new OpenApiPaths(),
        };

        var second = new OpenApiDocument
        {
            Components = new OpenApiComponents
            {
                Responses = new Dictionary<string, OpenApiResponse>
                {
                    ["Created"] = new() { Description = "Created B" },
                },
            },
            Paths = new OpenApiPaths(),
        };

        var config = new MergeConfiguration
        {
            Info = new MergeInfoConfiguration("Platform API", "1.0.0"),
            Sources =
            [
                new SourceConfiguration("first.yaml", Name: "First"),
                new SourceConfiguration("second.yaml", Name: "Second"),
            ],
        };
        config.Conflicts.Responses = new MergeComponentConflictPolicy(
            MergeDuplicateHandling.Dedupe,
            MergeConflictResolution.KeepIncoming);

        var merger = new OpenApiMerger();
        var result = merger.Merge(config, [(config.Sources[0], first), (config.Sources[1], second)]);

        Assert.Equal("Created B", result.Document.Components!.Responses!["Created"].Description);
    }

    [Fact]
    public void Merge_Should_Rename_Existing_Response_When_Configured()
    {
        var first = new OpenApiDocument
        {
            Components = new OpenApiComponents
            {
                Responses = new Dictionary<string, OpenApiResponse>
                {
                    ["Created"] = new() { Description = "Created A" },
                },
            },
            Paths = new OpenApiPaths
            {
                ["/a"] = new OpenApiPathItem
                {
                    Operations = new Dictionary<OperationType, OpenApiOperation>
                    {
                        [OperationType.Get] = new()
                        {
                            Responses = new OpenApiResponses
                            {
                                ["201"] = new OpenApiResponse
                                {
                                    Reference = new OpenApiReference { Type = ReferenceType.Response, Id = "Created" },
                                    UnresolvedReference = true,
                                },
                            },
                        },
                    },
                },
            },
        };

        var second = new OpenApiDocument
        {
            Components = new OpenApiComponents
            {
                Responses = new Dictionary<string, OpenApiResponse>
                {
                    ["Created"] = new() { Description = "Created B" },
                },
            },
            Paths = new OpenApiPaths
            {
                ["/b"] = new OpenApiPathItem
                {
                    Operations = new Dictionary<OperationType, OpenApiOperation>
                    {
                        [OperationType.Get] = new()
                        {
                            Responses = new OpenApiResponses
                            {
                                ["201"] = new OpenApiResponse
                                {
                                    Reference = new OpenApiReference { Type = ReferenceType.Response, Id = "Created" },
                                    UnresolvedReference = true,
                                },
                            },
                        },
                    },
                },
            },
        };

        var config = new MergeConfiguration
        {
            Info = new MergeInfoConfiguration("Platform API", "1.0.0"),
            Sources =
            [
                new SourceConfiguration("first.yaml", Name: "First"),
                new SourceConfiguration("second.yaml", Name: "Second"),
            ],
        };
        config.Conflicts.Responses = new MergeComponentConflictPolicy(MergeDuplicateHandling.Dedupe, MergeConflictResolution.RenameExisting);

        var merger = new OpenApiMerger();
        var result = merger.Merge(config, [(config.Sources[0], first), (config.Sources[1], second)]);

        Assert.Contains("First_Created", result.Document.Components!.Responses!.Keys);
        Assert.Contains("Created", result.Document.Components.Responses.Keys);
        Assert.Equal("First_Created", result.Document.Paths["/a"].Operations[OperationType.Get].Responses["201"].Reference!.Id);
        Assert.Equal("Created", result.Document.Paths["/b"].Operations[OperationType.Get].Responses["201"].Reference!.Id);
    }

    [Fact]
    public void Merge_Should_Rename_Both_Responses_When_Configured()
    {
        var first = new OpenApiDocument
        {
            Components = new OpenApiComponents
            {
                Responses = new Dictionary<string, OpenApiResponse>
                {
                    ["Created"] = new() { Description = "Created A" },
                },
            },
            Paths = new OpenApiPaths
            {
                ["/a"] = new OpenApiPathItem
                {
                    Operations = new Dictionary<OperationType, OpenApiOperation>
                    {
                        [OperationType.Get] = new()
                        {
                            Responses = new OpenApiResponses
                            {
                                ["201"] = new OpenApiResponse
                                {
                                    Reference = new OpenApiReference { Type = ReferenceType.Response, Id = "Created" },
                                    UnresolvedReference = true,
                                },
                            },
                        },
                    },
                },
            },
        };

        var second = new OpenApiDocument
        {
            Components = new OpenApiComponents
            {
                Responses = new Dictionary<string, OpenApiResponse>
                {
                    ["Created"] = new() { Description = "Created B" },
                },
            },
            Paths = new OpenApiPaths
            {
                ["/b"] = new OpenApiPathItem
                {
                    Operations = new Dictionary<OperationType, OpenApiOperation>
                    {
                        [OperationType.Get] = new()
                        {
                            Responses = new OpenApiResponses
                            {
                                ["201"] = new OpenApiResponse
                                {
                                    Reference = new OpenApiReference { Type = ReferenceType.Response, Id = "Created" },
                                    UnresolvedReference = true,
                                },
                            },
                        },
                    },
                },
            },
        };

        var config = new MergeConfiguration
        {
            Info = new MergeInfoConfiguration("Platform API", "1.0.0"),
            Sources =
            [
                new SourceConfiguration("first.yaml", Name: "First"),
                new SourceConfiguration("second.yaml", Name: "Second"),
            ],
        };
        config.Conflicts.Responses = new MergeComponentConflictPolicy(MergeDuplicateHandling.Dedupe, MergeConflictResolution.RenameBoth);

        var merger = new OpenApiMerger();
        var result = merger.Merge(config, [(config.Sources[0], first), (config.Sources[1], second)]);

        Assert.Contains("First_Created", result.Document.Components!.Responses!.Keys);
        Assert.Contains("Second_Created", result.Document.Components.Responses.Keys);
        Assert.DoesNotContain("Created", result.Document.Components.Responses.Keys);
        Assert.Equal("First_Created", result.Document.Paths["/a"].Operations[OperationType.Get].Responses["201"].Reference!.Id);
        Assert.Equal("Second_Created", result.Document.Paths["/b"].Operations[OperationType.Get].Responses["201"].Reference!.Id);
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
    public void Merge_Should_Enrich_Existing_Tag_Metadata()
    {
        var first = new OpenApiDocument
        {
            Tags = [new OpenApiTag { Name = "users" }],
            Paths = new OpenApiPaths(),
        };

        var second = new OpenApiDocument
        {
            Tags =
            [
                new OpenApiTag
                {
                    Name = "users",
                    Description = "User operations",
                    ExternalDocs = new OpenApiExternalDocs
                    {
                        Url = new Uri("https://example.com/users"),
                        Description = "Users docs",
                    },
                },
            ],
            Paths = new OpenApiPaths(),
        };

        var config = new MergeConfiguration
        {
            Info = new MergeInfoConfiguration("Platform API", "1.0.0"),
            Sources =
            [
                new SourceConfiguration("first.yaml", Name: "First"),
                new SourceConfiguration("second.yaml", Name: "Second"),
            ],
        };

        var merger = new OpenApiMerger();
        var result = merger.Merge(config, [(config.Sources[0], first), (config.Sources[1], second)]);

        var tag = Assert.Single(result.Document.Tags!);
        Assert.Equal("User operations", tag.Description);
        Assert.Equal(new Uri("https://example.com/users"), tag.ExternalDocs!.Url);
    }

    [Fact]
    public void Merge_Should_Deduplicate_Document_Security_Requirements()
    {
        var sharedScheme = new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ApiKey" },
            UnresolvedReference = true,
        };

        var requirement = new OpenApiSecurityRequirement
        {
            [sharedScheme] = ["read"],
        };

        var first = new OpenApiDocument
        {
            SecurityRequirements = [requirement],
            Paths = new OpenApiPaths(),
        };

        var second = new OpenApiDocument
        {
            SecurityRequirements =
            [
                new OpenApiSecurityRequirement
                {
                    [new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ApiKey" },
                        UnresolvedReference = true,
                    }] = ["read"],
                },
            ],
            Paths = new OpenApiPaths(),
        };

        var config = new MergeConfiguration
        {
            Info = new MergeInfoConfiguration("Platform API", "1.0.0"),
            Sources =
            [
                new SourceConfiguration("first.yaml", Name: "First"),
                new SourceConfiguration("second.yaml", Name: "Second"),
            ],
        };

        var merger = new OpenApiMerger();
        var result = merger.Merge(config, [(config.Sources[0], first), (config.Sources[1], second)]);

        Assert.Single(result.Document.SecurityRequirements!);
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
        var (result, _) = await MergeAsync(schemaConflict: MergeConflictResolution.RenameIncoming);

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
