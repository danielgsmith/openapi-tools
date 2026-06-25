using OpenApiTools.Models;
using OpenApiTools.Services;
using Xunit;

namespace OpenApiTools.Tests;

public class DifferTests
{
    private static string V1Path => Path.Combine(AppContext.BaseDirectory, "samples", "store-api-v1.yaml");
    private static string V2Path => Path.Combine(AppContext.BaseDirectory, "samples", "store-api-v2.yaml");

    [Fact]
    public async Task Diff_Should_Detect_Added_Path()
    {
        var loader = new OpenApiLoader();
        var oldDoc = await loader.LoadAsync(V1Path);
        var newDoc = await loader.LoadAsync(V2Path);
        var differ = new OpenApiDiffer();

        var changes = differ.Diff(oldDoc, newDoc);

        Assert.Contains(changes, c =>
            c.Category == DiffCategory.Path &&
            c.ChangeType == DiffChangeType.Added &&
            c.Location == "/orders");
    }

    [Fact]
    public async Task Diff_Should_Detect_Removed_Path()
    {
        var loader = new OpenApiLoader();
        var oldDoc = await loader.LoadAsync(V1Path);
        var newDoc = await loader.LoadAsync(V2Path);
        var differ = new OpenApiDiffer();

        var changes = differ.Diff(oldDoc, newDoc);

        Assert.Contains(changes, c =>
            c.Category == DiffCategory.Path &&
            c.ChangeType == DiffChangeType.Removed &&
            c.Location == "/search");
    }

    [Fact]
    public async Task Diff_Should_Detect_Removed_Path_As_Breaking()
    {
        var loader = new OpenApiLoader();
        var oldDoc = await loader.LoadAsync(V1Path);
        var newDoc = await loader.LoadAsync(V2Path);
        var differ = new OpenApiDiffer();

        var changes = differ.Diff(oldDoc, newDoc);

        Assert.Contains(changes, c =>
            c.Category == DiffCategory.Path &&
            c.ChangeType == DiffChangeType.Removed &&
            c.Impact == DiffImpact.Breaking);
    }

    [Fact]
    public async Task Diff_Should_Detect_Added_Path_As_NonBreaking()
    {
        var loader = new OpenApiLoader();
        var oldDoc = await loader.LoadAsync(V1Path);
        var newDoc = await loader.LoadAsync(V2Path);
        var differ = new OpenApiDiffer();

        var changes = differ.Diff(oldDoc, newDoc);

        Assert.Contains(changes, c =>
            c.Category == DiffCategory.Path &&
            c.ChangeType == DiffChangeType.Added &&
            c.Impact == DiffImpact.NonBreaking);
    }

    [Fact]
    public async Task Diff_Should_Detect_Schema_Type_Change_As_Breaking()
    {
        var loader = new OpenApiLoader();
        var oldDoc = await loader.LoadAsync(V1Path);
        var newDoc = await loader.LoadAsync(V2Path);
        var differ = new OpenApiDiffer();

        var changes = differ.Diff(oldDoc, newDoc);

        Assert.Contains(changes, c =>
            c.Category == DiffCategory.Schema &&
            c.ChangeType == DiffChangeType.Modified &&
            c.Impact == DiffImpact.Breaking &&
            c.Description.Contains("price") &&
            c.Description.Contains("type changed"));
    }

    [Fact]
    public async Task Diff_Should_Detect_Added_Required_Property_As_Breaking()
    {
        var loader = new OpenApiLoader();
        var oldDoc = await loader.LoadAsync(V1Path);
        var newDoc = await loader.LoadAsync(V2Path);
        var differ = new OpenApiDiffer();

        var changes = differ.Diff(oldDoc, newDoc);

        Assert.Contains(changes, c =>
            c.Category == DiffCategory.Schema &&
            c.Impact == DiffImpact.Breaking &&
            c.Description.Contains("required"));
    }

    [Fact]
    public async Task Diff_Should_Detect_Added_Parameter()
    {
        var loader = new OpenApiLoader();
        var oldDoc = await loader.LoadAsync(V1Path);
        var newDoc = await loader.LoadAsync(V2Path);
        var differ = new OpenApiDiffer();

        var changes = differ.Diff(oldDoc, newDoc);

        var paramChanges = changes.Where(c => c.Category == DiffCategory.Parameter);
        Assert.True(paramChanges.Count() >= 1, "Should detect parameter changes");
    }

    [Fact]
    public async Task Diff_Should_Detect_Added_Schema()
    {
        var loader = new OpenApiLoader();
        var oldDoc = await loader.LoadAsync(V1Path);
        var newDoc = await loader.LoadAsync(V2Path);
        var differ = new OpenApiDiffer();

        var changes = differ.Diff(oldDoc, newDoc);

        Assert.Contains(changes, c =>
            c.Category == DiffCategory.Schema &&
            c.ChangeType == DiffChangeType.Added);
    }

    [Fact]
    public async Task Diff_Should_Return_Empty_For_Identical_Docs()
    {
        var loader = new OpenApiLoader();
        var doc = await loader.LoadAsync(V1Path);
        var differ = new OpenApiDiffer();

        var changes = differ.Diff(doc, doc);

        Assert.Empty(changes);
    }
}