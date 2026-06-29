using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using OpenApiTools.Models;
using OpenApiTools.Services;
using Xunit;

namespace OpenApiTools.Tests;

public class MergeConflictServicesTests
{
    private readonly OpenApiComponentComparer _comparer = new();
    private readonly OpenApiConflictDetector _detector = new();

    [Fact]
    public void ComponentComparer_Should_Treat_Equivalent_Schemas_As_Equal()
    {
        var left = new OpenApiSchema
        {
            Type = "object",
            Properties = new Dictionary<string, OpenApiSchema>
            {
                ["name"] = new() { Type = "string" },
                ["id"] = new() { Type = "integer", Format = "int64" },
            },
            Required = new HashSet<string> { "id" },
        };

        var right = new OpenApiSchema
        {
            Required = new HashSet<string> { "id" },
            Properties = new Dictionary<string, OpenApiSchema>
            {
                ["id"] = new() { Type = "integer", Format = "int64" },
                ["name"] = new() { Type = "string" },
            },
            Type = "object",
        };

        Assert.True(_comparer.AreEquivalent(left, right));
    }

    [Fact]
    public void ComponentComparer_Should_Treat_Different_Schemas_As_Different()
    {
        var left = new OpenApiSchema { Type = "string" };
        var right = new OpenApiSchema { Type = "integer", Format = "int32" };

        Assert.False(_comparer.AreEquivalent(left, right));
    }

    [Fact]
    public void ConflictDetector_Should_Report_Unique_Component()
    {
        var components = new Dictionary<string, OpenApiSchema>(StringComparer.OrdinalIgnoreCase);
        var owners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var conflict = _detector.Detect(
            "schema",
            "User",
            "Users",
            components,
            owners,
            _comparer.AreEquivalent,
            new OpenApiSchema { Type = "object" });

        Assert.Equal(MergeConflictKind.Unique, conflict.Kind);
        Assert.Null(conflict.ExistingOwner);
    }

    [Fact]
    public void ConflictDetector_Should_Report_Identical_Duplicate_Component()
    {
        var components = new Dictionary<string, OpenApiSchema>(StringComparer.OrdinalIgnoreCase)
        {
            ["Error"] = new() { Type = "object", Properties = new Dictionary<string, OpenApiSchema> { ["message"] = new() { Type = "string" } } },
        };
        var owners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Error"] = "Users",
        };

        var conflict = _detector.Detect(
            "schema",
            "Error",
            "Products",
            components,
            owners,
            _comparer.AreEquivalent,
            new OpenApiSchema { Type = "object", Properties = new Dictionary<string, OpenApiSchema> { ["message"] = new() { Type = "string" } } });

        Assert.Equal(MergeConflictKind.IdenticalDuplicate, conflict.Kind);
        Assert.Equal("Users", conflict.ExistingOwner);
    }

    [Fact]
    public void ConflictDetector_Should_Report_Conflicting_Duplicate_Component()
    {
        var components = new Dictionary<string, OpenApiResponse>(StringComparer.OrdinalIgnoreCase)
        {
            ["ErrorResponse"] = new()
            {
                Description = "User error",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new() { Schema = new OpenApiSchema { Type = "string" } },
                },
            },
        };
        var owners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ErrorResponse"] = "Users",
        };

        var incoming = new OpenApiResponse
        {
            Description = "Product error",
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/json"] = new() { Schema = new OpenApiSchema { Type = "integer" } },
            },
        };

        var conflict = _detector.Detect(
            "response",
            "ErrorResponse",
            "Products",
            components,
            owners,
            _comparer.AreEquivalent,
            incoming);

        Assert.Equal(MergeConflictKind.ConflictingDuplicate, conflict.Kind);
        Assert.Equal("Users", conflict.ExistingOwner);
    }
}
