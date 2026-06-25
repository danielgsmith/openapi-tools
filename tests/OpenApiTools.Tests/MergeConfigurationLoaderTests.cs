using OpenApiTools.Models;
using Xunit;

namespace OpenApiTools.Tests;

public class MergeConfigurationLoaderTests
{
    [Fact]
    public void Load_Should_Parse_SchemaConflict_Enum_From_String()
    {
        var path = WriteTempJson(
            """
            {
              "info": { "title": "API", "version": "1.0.0" },
              "sources": [{ "path": "./a.yaml" }],
              "schemaConflict": "first-wins"
            }
            """);

        try
        {
            var config = MergeConfigurationLoader.Load(path);
            Assert.Equal(SchemaConflictStrategy.FirstWins, config.SchemaConflict);
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
