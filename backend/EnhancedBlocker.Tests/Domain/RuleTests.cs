using EnhancedBlocker.Domain;
using Xunit;

namespace EnhancedBlocker.Tests.Domain;

public class RuleTests
{
    [Fact]
    public void Create_WithValidDomainPattern_Succeeds_AndLowercases()
    {
        var result = Rule.Create("YouTube.com", MatchKind.Domain, RuleKind.Block, RuleSource.Manual, null);

        Assert.True(result.IsT0);
        Assert.Equal("youtube.com", result.AsT0.Pattern);
        Assert.Equal(RuleKind.Block, result.AsT0.Kind);
    }

    [Fact]
    public void Create_WithExactPattern_PreservesCase()
    {
        var result = Rule.Create("https://Example.com/Path", MatchKind.Exact, RuleKind.Allow, RuleSource.Manual, "work");

        Assert.True(result.IsT0);
        Assert.Equal("https://Example.com/Path", result.AsT0.Pattern);
        Assert.Equal("work", result.AsT0.Category);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankPattern_ReturnsValidationError(string pattern)
    {
        var result = Rule.Create(pattern, MatchKind.Domain, RuleKind.Block, RuleSource.Manual, null);

        Assert.True(result.IsT1);
    }

    [Fact]
    public void Update_ChangesFields_AndValidates()
    {
        var rule = Rule.Create("a.com", MatchKind.Domain, RuleKind.Block, RuleSource.Manual, null).AsT0;

        var updated = rule.Update("B.com", MatchKind.Domain, RuleKind.Allow, RuleSource.Derived, " news ");

        Assert.True(updated.IsT0);
        Assert.Equal("b.com", rule.Pattern);
        Assert.Equal(RuleKind.Allow, rule.Kind);
        Assert.Equal("news", rule.Category);
    }

    [Fact]
    public void Update_WithBlankPattern_ReturnsValidationError_AndKeepsState()
    {
        var rule = Rule.Create("a.com", MatchKind.Domain, RuleKind.Block, RuleSource.Manual, null).AsT0;

        var updated = rule.Update("", MatchKind.Domain, RuleKind.Block, RuleSource.Manual, null);

        Assert.True(updated.IsT1);
        Assert.Equal("a.com", rule.Pattern);
    }
}
