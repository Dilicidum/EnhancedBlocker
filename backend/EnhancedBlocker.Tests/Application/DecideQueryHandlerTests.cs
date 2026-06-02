using OneOf;
using EnhancedBlocker.Application.Decisions;
using EnhancedBlocker.Application.Ports;
using EnhancedBlocker.Domain;
using EnhancedBlocker.Tests.Fakes;
using Xunit;

namespace EnhancedBlocker.Tests.Application;

public class DecideQueryHandlerTests
{
    private static DecisionContext Ctx(string url = "https://x.com", string domain = "x.com") =>
        new(url, domain, null, null, null, null, DateTimeOffset.UtcNow);

    private static DecideQuery Query() => new(Ctx());

    [Fact]
    public async Task NoTiers_DefaultsToAllow()
    {
        var handler = new DecideQueryHandler([]);

        var result = await handler.Handle(Query(), CancellationToken.None);

        Assert.True(result.IsT0);
        Assert.Equal(Outcome.Allow, result.AsT0.Outcome);
        Assert.Equal("default", result.AsT0.Tier);
    }

    [Fact]
    public async Task FirstDecisiveTier_Wins()
    {
        var blocking = new StubTier(0, new TierResult(Outcome.Block, "tierA", "blocked", null));
        var allowing = new StubTier(1, new TierResult(Outcome.Allow, "tierB", "allowed", null));

        var handler = new DecideQueryHandler([allowing, blocking]); // unordered on purpose

        var result = await handler.Handle(Query(), CancellationToken.None);

        Assert.True(result.IsT0);
        Assert.Equal(Outcome.Block, result.AsT0.Outcome);
        Assert.Equal("tierA", result.AsT0.Tier);
    }

    [Fact]
    public async Task DeferringTier_FallsThroughToNext()
    {
        var defer = new StubTier(0, new Defer());
        var block = new StubTier(1, new TierResult(Outcome.Block, "tierB", "blocked", null));

        var handler = new DecideQueryHandler([defer, block]);

        var result = await handler.Handle(Query(), CancellationToken.None);

        Assert.True(result.IsT0);
        Assert.Equal(Outcome.Block, result.AsT0.Outcome);
    }

    [Fact]
    public async Task BlankUrl_ReturnsValidationError()
    {
        var handler = new DecideQueryHandler([]);
        var query = new DecideQuery(Ctx(url: "  "));

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsT1);
    }
}
