using OpenApiTools.Services;
using Xunit;

namespace OpenApiTools.Tests;

public class FuzzyMatcherTests
{
    [Theory]
    [InlineData("pet", "pet", 1.0)]
    [InlineData("pet", "Pet", 1.0)]
    [InlineData("pet", "petstore", 0.85)]
    [InlineData("pet", "petStore", 0.85)]
    [InlineData("pet", "pats", 0.5)]
    [InlineData("xyz", "abc", 0.0)]
    public void Score_Should_ReturnExpected(string query, string target, double minExpected)
    {
        var score = FuzzyMatcher.Score(query, target);
        Assert.True(score >= minExpected, $"Score {score} should be >= {minExpected}");
    }

    [Fact]
    public void Matches_Should_ReturnTrue_ForSubstring()
    {
        Assert.True(FuzzyMatcher.Matches("pet", "petstore"));
    }

    [Fact]
    public void Matches_Should_ReturnFalse_ForUnrelated()
    {
        Assert.False(FuzzyMatcher.Matches("xyz", "abc"));
    }

    [Fact]
    public void LevenshteinDistance_Should_ComputeCorrectly()
    {
        Assert.Equal(0, FuzzyMatcher.LevenshteinDistance("hello", "hello"));
        Assert.Equal(1, FuzzyMatcher.LevenshteinDistance("hello", "helo"));
        Assert.Equal(5, FuzzyMatcher.LevenshteinDistance("", "hello"));
    }
}