using EnhancedBlocker.Application.Decisions;
using EnhancedBlocker.Domain;
using EnhancedBlocker.Infrastructure.Decisions;
using EnhancedBlocker.Tests.Fakes;
using Xunit;

namespace EnhancedBlocker.Tests.Infrastructure;

public class Tier0RuleTierTests
{
    private static DecisionContext Ctx(string url, string domain) =>
        new(url, domain, null, null, null, null, DateTimeOffset.UtcNow);

    private static Rule R(string pattern, MatchKind match, RuleKind kind) =>
        Rule.Create(pattern, match, kind, RuleSource.Manual, null).AsT0;

    [Fact]
    public async Task DomainRule_MatchesExactDomain_Blocks()
    {
        var tier = new Tier0RuleTier(
            new FakeRuleRepository([R("youtube.com", MatchKind.Domain, RuleKind.Block)]),
            new FakeCategoryDomainCache());

        var result = await tier.EvaluateAsync(
            Ctx("https://youtube.com/watch?v=1", "youtube.com"), CancellationToken.None);

        Assert.True(result.IsT0);
        Assert.Equal(Outcome.Block, result.AsT0.Outcome);
        Assert.Equal("tier0", result.AsT0.Tier);
    }

    [Fact]
    public async Task DomainRule_MatchesSubdomain_Blocks()
    {
        var tier = new Tier0RuleTier(
            new FakeRuleRepository([R("youtube.com", MatchKind.Domain, RuleKind.Block)]),
            new FakeCategoryDomainCache());

        var result = await tier.EvaluateAsync(
            Ctx("https://m.youtube.com/", "m.youtube.com"), CancellationToken.None);

        Assert.True(result.IsT0);
        Assert.Equal(Outcome.Block, result.AsT0.Outcome);
    }

    [Fact]
    public async Task DomainRule_DoesNotMatchUnrelatedDomain_Defers()
    {
        var tier = new Tier0RuleTier(
            new FakeRuleRepository([R("youtube.com", MatchKind.Domain, RuleKind.Block)]),
            new FakeCategoryDomainCache());

        var result = await tier.EvaluateAsync(
            Ctx("https://github.com/", "github.com"), CancellationToken.None);

        Assert.True(result.IsT1); // Defer
    }

    [Fact]
    public async Task DomainRule_DoesNotMatchLookalikeSuffix_Defers()
    {
        // "notyoutube.com" must NOT match a "youtube.com" domain rule.
        var tier = new Tier0RuleTier(
            new FakeRuleRepository([R("youtube.com", MatchKind.Domain, RuleKind.Block)]),
            new FakeCategoryDomainCache());

        var result = await tier.EvaluateAsync(
            Ctx("https://notyoutube.com/", "notyoutube.com"), CancellationToken.None);

        Assert.True(result.IsT1); // Defer
    }

    [Fact]
    public async Task ExactRule_MatchesFullUrl_Blocks()
    {
        var tier = new Tier0RuleTier(
            new FakeRuleRepository([R("https://example.com/bad", MatchKind.Exact, RuleKind.Block)]),
            new FakeCategoryDomainCache());

        var result = await tier.EvaluateAsync(
            Ctx("https://example.com/bad", "example.com"), CancellationToken.None);

        Assert.True(result.IsT0);
        Assert.Equal(Outcome.Block, result.AsT0.Outcome);
    }

    [Fact]
    public async Task ExactRule_DoesNotMatchDifferentPath_Defers()
    {
        var tier = new Tier0RuleTier(
            new FakeRuleRepository([R("https://example.com/bad", MatchKind.Exact, RuleKind.Block)]),
            new FakeCategoryDomainCache());

        var result = await tier.EvaluateAsync(
            Ctx("https://example.com/good", "example.com"), CancellationToken.None);

        Assert.True(result.IsT1); // Defer
    }

    [Fact]
    public async Task AllowRule_TakesPrecedenceOverBlockRule()
    {
        var tier = new Tier0RuleTier(
            new FakeRuleRepository(
            [
                R("youtube.com", MatchKind.Domain, RuleKind.Block),
                R("youtube.com", MatchKind.Domain, RuleKind.Allow)
            ]),
            new FakeCategoryDomainCache());

        var result = await tier.EvaluateAsync(
            Ctx("https://youtube.com/", "youtube.com"), CancellationToken.None);

        Assert.True(result.IsT0);
        Assert.Equal(Outcome.Allow, result.AsT0.Outcome);
    }

    [Fact]
    public async Task CategoryCache_DomainPresent_Blocks()
    {
        var cat = CategoryDomain.Create("news.com", "news", 0.95, DateTimeOffset.UtcNow).AsT0;
        var tier = new Tier0RuleTier(
            new FakeRuleRepository(),
            new FakeCategoryDomainCache(new Dictionary<string, CategoryDomain> { ["news.com"] = cat }));

        var result = await tier.EvaluateAsync(
            Ctx("https://news.com/article", "news.com"), CancellationToken.None);

        Assert.True(result.IsT0);
        Assert.Equal(Outcome.Block, result.AsT0.Outcome);
        Assert.Equal(0.95, result.AsT0.Score);
    }

    [Fact]
    public async Task NoRulesNoCache_Defers()
    {
        var tier = new Tier0RuleTier(new FakeRuleRepository(), new FakeCategoryDomainCache());

        var result = await tier.EvaluateAsync(
            Ctx("https://anything.com/", "anything.com"), CancellationToken.None);

        Assert.True(result.IsT1); // Defer
    }
}
