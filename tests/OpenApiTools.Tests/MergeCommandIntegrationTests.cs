using System.Diagnostics;
using Xunit;

namespace OpenApiTools.Tests;

public class MergeCommandIntegrationTests
{
    private static string RepoRoot => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    private static string ToolDll
    {
        get
        {
            var tfmDir = new DirectoryInfo(AppContext.BaseDirectory);
            var configuration = tfmDir.Parent?.Name ?? "Debug";
            var candidate = Path.Combine(RepoRoot, "src", "OpenApiTools", "bin", configuration, "net10.0", "OpenApiTools.dll");
            if (File.Exists(candidate))
                return candidate;

            var debugFallback = Path.Combine(RepoRoot, "src", "OpenApiTools", "bin", "Debug", "net10.0", "OpenApiTools.dll");
            if (File.Exists(debugFallback))
                return debugFallback;

            var releaseFallback = Path.Combine(RepoRoot, "src", "OpenApiTools", "bin", "Release", "net10.0", "OpenApiTools.dll");
            return releaseFallback;
        }
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunToolAsync(params string[] args)
    {
        var psi = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = RepoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add(ToolDll);
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi)!;
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, stdout, stderr);
    }

    private static string WriteTempFile(string extension, string content)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + extension);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public async Task Merge_DirectMode_Should_Write_Output_File()
    {
        var output = Path.Combine(Path.GetTempPath(), "merged-direct-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var result = await RunToolAsync(
                "merge",
                "--title", "Platform API",
                "--version", "1.0.0",
                "-o", output,
                "samples/users-api.yaml",
                "samples/products-api.yaml");

            Assert.Equal(0, result.ExitCode);
            Assert.True(File.Exists(output));
            Assert.Contains("Merged 2 file(s)", result.StdOut + result.StdErr);
        }
        finally
        {
            File.Delete(output);
        }
    }

    [Fact]
    public async Task Merge_ConfigMode_Should_Write_Output_File()
    {
        var output = Path.Combine(RepoRoot, "merged-platform.json");
        try
        {
            var result = await RunToolAsync("merge", "--config", "samples/merge-config.json");

            Assert.Equal(0, result.ExitCode);
            Assert.True(File.Exists(output));
        }
        finally
        {
            File.Delete(output);
        }
    }

    [Fact]
    public async Task Merge_Should_Support_Yaml_Output()
    {
        var output = Path.Combine(Path.GetTempPath(), "merged-yaml-" + Guid.NewGuid().ToString("N") + ".yaml");
        try
        {
            var result = await RunToolAsync(
                "merge",
                "--title", "Platform API",
                "--version", "1.0.0",
                "--format", "yaml",
                "-o", output,
                "samples/users-api.yaml",
                "samples/products-api.yaml");

            Assert.Equal(0, result.ExitCode);
            Assert.True(File.Exists(output));
            var content = await File.ReadAllTextAsync(output);
            Assert.Contains("openapi:", content);
        }
        finally
        {
            File.Delete(output);
        }
    }

    [Fact]
    public async Task Merge_Should_Fail_On_Invalid_Schema_Conflict_Value()
    {
        var result = await RunToolAsync(
            "merge",
            "--title", "Platform API",
            "--version", "1.0.0",
            "--schema-conflict", "banana",
            "samples/users-api.yaml",
            "samples/products-api.yaml");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("banana", result.StdErr + result.StdOut);
        Assert.Contains("--schema-conflict", result.StdErr + result.StdOut);
    }

    [Fact]
    public async Task Merge_Should_Fail_On_Invalid_Schema_Identical_Value()
    {
        var result = await RunToolAsync(
            "merge",
            "--title", "Platform API",
            "--version", "1.0.0",
            "--schema-identical", "banana",
            "samples/users-api.yaml",
            "samples/products-api.yaml");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("banana", result.StdErr + result.StdOut);
        Assert.Contains("--schema-identical", result.StdErr + result.StdOut);
    }

    [Fact]
    public async Task Merge_Should_Fail_On_Invalid_Output_Format()
    {
        var result = await RunToolAsync(
            "merge",
            "--title", "Platform API",
            "--version", "1.0.0",
            "--format", "toml",
            "samples/users-api.yaml",
            "samples/products-api.yaml");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("toml", result.StdErr + result.StdOut);
        Assert.Contains("--format", result.StdErr + result.StdOut);
    }

    [Fact]
    public async Task Merge_Should_Combine_Different_Methods_On_Same_Path_EndToEnd()
    {
        var first = WriteTempFile(
            ".yaml",
            """
            openapi: 3.0.1
            info:
              title: First
              version: 1.0.0
            paths:
              /shared:
                get:
                  operationId: getShared
                  responses:
                    '200':
                      description: OK
            """);
        var second = WriteTempFile(
            ".yaml",
            """
            openapi: 3.0.1
            info:
              title: Second
              version: 1.0.0
            paths:
              /shared:
                post:
                  operationId: postShared
                  responses:
                    '201':
                      description: Created
            """);
        var output = Path.Combine(Path.GetTempPath(), "merged-path-methods-" + Guid.NewGuid().ToString("N") + ".json");

        try
        {
            var result = await RunToolAsync(
                "merge",
                "--title", "Platform API",
                "--version", "1.0.0",
                "-o", output,
                first,
                second);

            Assert.Equal(0, result.ExitCode);
            Assert.True(File.Exists(output));

            var endpoints = await RunToolAsync("endpoints", output, "--format", "plain");
            Assert.Contains("GET\t/shared\tgetShared", endpoints.StdOut);
            Assert.Contains("POST\t/shared\tpostShared", endpoints.StdOut);
        }
        finally
        {
            File.Delete(first);
            File.Delete(second);
            File.Delete(output);
        }
    }

    [Fact]
    public async Task Merge_Should_Deduplicate_Identical_Schemas_EndToEnd()
    {
        var first = WriteTempFile(
            ".yaml",
            """
            openapi: 3.0.1
            info:
              title: First
              version: 1.0.0
            components:
              schemas:
                Error:
                  type: object
                  properties:
                    message:
                      type: string
            paths: {}
            """);
        var second = WriteTempFile(
            ".yaml",
            """
            openapi: 3.0.1
            info:
              title: Second
              version: 1.0.0
            components:
              schemas:
                Error:
                  properties:
                    message:
                      type: string
                  type: object
            paths: {}
            """);
        var output = Path.Combine(Path.GetTempPath(), "merged-identical-schemas-" + Guid.NewGuid().ToString("N") + ".json");

        try
        {
            var result = await RunToolAsync(
                "merge",
                "--title", "Platform API",
                "--version", "1.0.0",
                "-o", output,
                first,
                second);

            Assert.Equal(0, result.ExitCode);
            var content = await File.ReadAllTextAsync(output);
            Assert.Contains("\"Error\"", content);
            Assert.DoesNotContain("Second_Error", content);
        }
        finally
        {
            File.Delete(first);
            File.Delete(second);
            File.Delete(output);
        }
    }

    [Fact]
    public async Task Merge_DirectMode_Should_Fail_When_Title_Is_Missing()
    {
        var result = await RunToolAsync(
            "merge",
            "--version", "1.0.0",
            "samples/users-api.yaml",
            "samples/products-api.yaml");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("--title is required", result.StdErr + result.StdOut);
    }

    [Fact]
    public async Task Merge_DirectMode_Should_Fail_When_Version_Is_Missing()
    {
        var result = await RunToolAsync(
            "merge",
            "--title", "Platform API",
            "samples/users-api.yaml",
            "samples/products-api.yaml");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("--version is required", result.StdErr + result.StdOut);
    }

    [Fact]
    public async Task Merge_Should_Return_NonZero_On_Fail_Strategy_Conflict()
    {
        var output = Path.Combine(Path.GetTempPath(), "merged-fail-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var result = await RunToolAsync(
                "merge",
                "--title", "Platform API",
                "--version", "1.0.0",
                "--schema-conflict", "fail",
                "-o", output,
                "samples/users-api.yaml",
                "samples/products-api.yaml");

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("Schema conflict", result.StdErr + result.StdOut);
            Assert.False(File.Exists(output));
        }
        finally
        {
            File.Delete(output);
        }
    }

    [Fact]
    public async Task Merge_ConfigMode_Should_Apply_Path_And_OperationId_Prefixes()
    {
        var output = Path.Combine(RepoRoot, "merged-platform.json");
        try
        {
            var result = await RunToolAsync("merge", "--config", "samples/merge-config.json");
            Assert.Equal(0, result.ExitCode);
            Assert.True(File.Exists(output));

            var endpoints = await RunToolAsync("endpoints", output, "--format", "plain");
            Assert.Contains("/users/users", endpoints.StdOut);
            Assert.Contains("users_listUsers", endpoints.StdOut);
            Assert.Contains("/products/products", endpoints.StdOut);
            Assert.Contains("products_listProducts", endpoints.StdOut);
        }
        finally
        {
            File.Delete(output);
        }
    }

    [Fact]
    public async Task Merge_Verbose_Should_Report_Warnings()
    {
        var output = Path.Combine(Path.GetTempPath(), "merged-verbose-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var result = await RunToolAsync(
                "merge",
                "-v",
                "--title", "Platform API",
                "--version", "1.0.0",
                "-o", output,
                "samples/users-api.yaml",
                "samples/products-api.yaml");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Loading 2 source file(s)", result.StdOut + result.StdErr);
            Assert.Contains("WARN:", result.StdOut + result.StdErr);
        }
        finally
        {
            File.Delete(output);
        }
    }
}
