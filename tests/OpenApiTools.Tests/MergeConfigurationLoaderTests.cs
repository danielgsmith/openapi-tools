using OpenApiTools.Models;
using Xunit;

namespace OpenApiTools.Tests;

public class MergeConfigurationLoaderTests
{
    [Fact]
    public void Load_Should_Parse_Schema_Conflict_Policy_From_Config()
    {
        var path = WriteTempJson(
            """
            {
              "info": { "title": "API", "version": "1.0.0" },
              "sources": [{ "path": "./a.yaml" }],
              "conflicts": {
                "schemas": {
                  "conflict": "keep-existing"
                }
              }
            }
            """);

        try
        {
            var config = MergeConfigurationLoader.Load(path);
            Assert.Equal(MergeConflictResolution.KeepExisting, config.Conflicts.Schemas.Conflict);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_Should_Parse_Rich_Component_Conflict_Policies()
    {
        var path = WriteTempJson(
            """
            {
              "info": { "title": "API", "version": "1.0.0" },
              "sources": [{ "path": "./a.yaml" }],
              "conflicts": {
                "schemas": {
                  "identical": "warn-and-dedupe",
                  "conflict": "rename-incoming"
                },
                "responses": {
                  "identical": "fail",
                  "conflict": "rename-existing"
                }
              }
            }
            """);

        try
        {
            var config = MergeConfigurationLoader.Load(path);
            Assert.Equal(MergeDuplicateHandling.WarnAndDedupe, config.Conflicts.Schemas.Identical);
            Assert.Equal(MergeConflictResolution.RenameIncoming, config.Conflicts.Schemas.Conflict);
            Assert.Equal(MergeDuplicateHandling.Fail, config.Conflicts.Responses.Identical);
            Assert.Equal(MergeConflictResolution.RenameExisting, config.Conflicts.Responses.Conflict);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_Should_Fail_On_Invalid_Conflict_Policy_Value()
    {
        var path = WriteTempJson(
            """
            {
              "info": { "title": "API", "version": "1.0.0" },
              "sources": [{ "path": "./a.yaml" }],
              "conflicts": {
                "schemas": {
                  "conflict": "totally-unknown"
                }
              }
            }
            """);

        try
        {
            var ex = Assert.Throws<InvalidOperationException>(() => MergeConfigurationLoader.Load(path));
            Assert.Contains("totally-unknown", ex.Message);
            Assert.Contains("schemas.conflict", ex.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_Should_Fail_When_Sources_Are_Missing()
    {
        var path = WriteTempJson(
            """
            {
              "info": { "title": "API", "version": "1.0.0" },
              "sources": []
            }
            """);

        try
        {
            var ex = Assert.Throws<InvalidOperationException>(() => MergeConfigurationLoader.Load(path));
            Assert.Contains("at least one source", ex.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_Should_Fail_When_Title_Is_Missing()
    {
        var path = WriteTempJson(
            """
            {
              "info": { "version": "1.0.0" },
              "sources": [{ "path": "./a.yaml" }]
            }
            """);

        try
        {
            var ex = Assert.Throws<InvalidOperationException>(() => MergeConfigurationLoader.Load(path));
            Assert.Contains("info.title", ex.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_Should_Fail_When_Version_Is_Missing()
    {
        var path = WriteTempJson(
            """
            {
              "info": { "title": "API" },
              "sources": [{ "path": "./a.yaml" }]
            }
            """);

        try
        {
            var ex = Assert.Throws<InvalidOperationException>(() => MergeConfigurationLoader.Load(path));
            Assert.Contains("info.version", ex.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WriteTempJson(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), "merge-config-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, json);
        return path;
    }
}
