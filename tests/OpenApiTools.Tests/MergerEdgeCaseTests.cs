using Microsoft.OpenApi.Models;
using OpenApiTools.Models;
using OpenApiTools.Services;
using Xunit;

namespace OpenApiTools.Tests;

public class MergerEdgeCaseTests
{
    private static string SamplesDir => Path.Combine(AppContext.BaseDirectory, "samples");

    private static async Task<OpenApiDocument> LoadAsync(string fileName)
        => await new OpenApiLoader().LoadAsync(Path.Combine(SamplesDir, fileName));

    [Fact]
    public async Task Merge_Rename_Should_Rewrite_Response_Schema_Refs()
    {
        var a = await LoadAsync("merge-ref-a.yaml");
        var b = await LoadAsync("merge-ref-b.yaml");
        var merger = new OpenApiMerger();
        var config = new MergeConfiguration
        {
            Info = new MergeInfoConfiguration("Merged", "1.0.0"),
            SchemaConflict = SchemaConflictStrategy.Rename,
            Sources =
            [
                new SourceConfiguration("merge-ref-a.yaml", Name: "A"),
                new SourceConfiguration("merge-ref-b.yaml", Name: "B"),
            ],
        };

        var result = merger.Merge(config, [(config.Sources[0], a), (config.Sources[1], b)]);

        Assert.Contains("ResponseEnvelope", result.Document.Components!.Schemas!.Keys);
        Assert.Contains("B_ResponseEnvelope", result.Document.Components.Schemas.Keys);

        var schemaA = result.Document.Paths["/a/items"].Operations[OperationType.Get].Responses["200"].Content["application/json"].Schema;
        var schemaB = result.Document.Paths["/b/items"].Operations[OperationType.Get].Responses["200"].Content["application/json"].Schema;

        Assert.NotNull(schemaA.Reference);
        Assert.NotNull(schemaB.Reference);
        Assert.Equal("ResponseEnvelope", schemaA.Reference!.Id);
        Assert.Equal("B_ResponseEnvelope", schemaB.Reference!.Id);
    }

    [Fact]
    public async Task Merge_Should_Merge_Parameter_Response_And_RequestBody_Components()
    {
        var a = await LoadAsync("merge-components-a.yaml");
        var b = await LoadAsync("merge-components-b.yaml");
        var merger = new OpenApiMerger();
        var config = new MergeConfiguration
        {
            Info = new MergeInfoConfiguration("Merged", "1.0.0"),
            Sources =
            [
                new SourceConfiguration("merge-components-a.yaml", Name: "A"),
                new SourceConfiguration("merge-components-b.yaml", Name: "B"),
            ],
        };

        var result = merger.Merge(config, [(config.Sources[0], a), (config.Sources[1], b)]);

        Assert.Contains("TraceId", result.Document.Components!.Parameters!.Keys);
        Assert.Contains("TenantId", result.Document.Components.Parameters.Keys);
        Assert.Contains("AlphaCreated", result.Document.Components.Responses!.Keys);
        Assert.Contains("BetaCreated", result.Document.Components.Responses.Keys);
        Assert.Contains("AlphaBody", result.Document.Components.RequestBodies!.Keys);
        Assert.Contains("BetaBody", result.Document.Components.RequestBodies.Keys);

        var alphaOp = result.Document.Paths["/alpha"].Operations[OperationType.Post];
        Assert.Equal("TraceId", alphaOp.Parameters![0].Reference!.Id);
        Assert.Equal("AlphaBody", alphaOp.RequestBody!.Reference!.Id);
        Assert.Equal("AlphaCreated", alphaOp.Responses["201"].Reference!.Id);

        var betaOp = result.Document.Paths["/beta"].Operations[OperationType.Post];
        Assert.Equal("TenantId", betaOp.Parameters![0].Reference!.Id);
        Assert.Equal("BetaBody", betaOp.RequestBody!.Reference!.Id);
        Assert.Equal("BetaCreated", betaOp.Responses["201"].Reference!.Id);
    }

    [Fact]
    public async Task Merge_Should_Warn_And_Keep_First_On_Path_Collision()
    {
        var a = await LoadAsync("merge-path-collision-a.yaml");
        var b = await LoadAsync("merge-path-collision-b.yaml");
        var merger = new OpenApiMerger();
        var config = new MergeConfiguration
        {
            Info = new MergeInfoConfiguration("Merged", "1.0.0"),
            Sources =
            [
                new SourceConfiguration("merge-path-collision-a.yaml", Name: "A"),
                new SourceConfiguration("merge-path-collision-b.yaml", Name: "B"),
            ],
        };

        var result = merger.Merge(config, [(config.Sources[0], a), (config.Sources[1], b)]);

        Assert.Single(result.Document.Paths);
        Assert.Contains(result.Warnings, w => w.Message.Contains("/shared") && w.Message.Contains("skipping"));
        Assert.Equal("getSharedA", result.Document.Paths["/shared"].Operations[OperationType.Get].OperationId);
    }

    [Fact]
    public async Task Merge_Should_Warn_On_Duplicate_OperationIds()
    {
        var a = await LoadAsync("merge-dup-opid-a.yaml");
        var b = await LoadAsync("merge-dup-opid-b.yaml");
        var merger = new OpenApiMerger();
        var config = new MergeConfiguration
        {
            Info = new MergeInfoConfiguration("Merged", "1.0.0"),
            Sources =
            [
                new SourceConfiguration("merge-dup-opid-a.yaml", Name: "A"),
                new SourceConfiguration("merge-dup-opid-b.yaml", Name: "B"),
            ],
        };

        var result = merger.Merge(config, [(config.Sources[0], a), (config.Sources[1], b)]);

        Assert.Equal(2, result.Document.Paths.Count);
        Assert.Contains(result.Warnings, w => w.Message.Contains("Duplicate operationId 'duplicateId'"));
    }

    [Theory]
    [InlineData("users")]
    [InlineData("/users")]
    [InlineData("/users/")]
    public async Task Merge_Should_Normalize_Path_Prefixes(string prefix)
    {
        var a = await LoadAsync("users-api.yaml");
        var merger = new OpenApiMerger();
        var config = new MergeConfiguration
        {
            Info = new MergeInfoConfiguration("Merged", "1.0.0"),
            Sources = [new SourceConfiguration("users-api.yaml", PathPrefix: prefix, Name: "Users")],
        };

        var result = merger.Merge(config, [(config.Sources[0], a)]);

        Assert.Contains("/users/users", result.Document.Paths.Keys);
        Assert.Contains("/users/users/{userId}", result.Document.Paths.Keys);
    }

    [Fact]
    public async Task Merge_Should_Use_Filename_As_Fallback_Source_Name_For_Rename()
    {
        var a = await LoadAsync("merge-ref-a.yaml");
        var b = await LoadAsync("merge-ref-b.yaml");
        var merger = new OpenApiMerger();
        var config = new MergeConfiguration
        {
            Info = new MergeInfoConfiguration("Merged", "1.0.0"),
            SchemaConflict = SchemaConflictStrategy.Rename,
            Sources =
            [
                new SourceConfiguration("merge-ref-a.yaml"),
                new SourceConfiguration("merge-ref-b.yaml"),
            ],
        };

        var result = merger.Merge(config, [(config.Sources[0], a), (config.Sources[1], b)]);

        Assert.Contains("merge_ref_b_ResponseEnvelope", result.Document.Components!.Schemas!.Keys);
    }
}
