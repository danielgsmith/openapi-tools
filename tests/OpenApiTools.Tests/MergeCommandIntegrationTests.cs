using System.Diagnostics;
using Xunit;

namespace OpenApiTools.Tests;

public class MergeCommandIntegrationTests
{
    private static string RepoRoot => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    private static string ToolDll => Path.Combine(RepoRoot, "src", "OpenApiTools", "bin", "Debug", "net10.0", "OpenApiTools.dll");

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
