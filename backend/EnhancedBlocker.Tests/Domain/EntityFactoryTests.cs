using EnhancedBlocker.Domain;
using Xunit;

namespace EnhancedBlocker.Tests.Domain;

public class EntityFactoryTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void Event_Create_Valid_Succeeds_AndNormalizesDomain()
    {
        var result = Event.Create(Now, "https://A.com/x", "A.com", "Title", 7, EventType.Navigate, null, 1000);

        Assert.True(result.IsT0);
        Assert.Equal("a.com", result.AsT0.Domain);
        Assert.Equal(1000, result.AsT0.DurationMs);
    }

    [Fact]
    public void Event_Create_NegativeDuration_Fails()
    {
        var result = Event.Create(Now, "https://a.com", "a.com", null, 1, EventType.Active, null, -5);
        Assert.True(result.IsT1);
    }

    [Fact]
    public void Event_Create_BlankUrl_Fails()
    {
        var result = Event.Create(Now, "", "a.com", null, 1, EventType.Navigate, null, null);
        Assert.True(result.IsT1);
    }

    [Fact]
    public void FocusSession_Create_Valid_Succeeds_WithNullEmbedding()
    {
        var result = FocusSession.Create(Now, "writing a parser");

        Assert.True(result.IsT0);
        Assert.Null(result.AsT0.EndedAt);
        Assert.Null(result.AsT0.IntentEmbedding);
    }

    [Fact]
    public void FocusSession_Create_BlankIntent_Fails()
    {
        var result = FocusSession.Create(Now, "  ");
        Assert.True(result.IsT1);
    }

    [Fact]
    public void FocusSession_Stop_Once_Succeeds_SecondTimeFails()
    {
        var session = FocusSession.Create(Now, "x").AsT0;

        var first = session.Stop(Now.AddMinutes(5));
        Assert.True(first.IsT0);
        Assert.NotNull(session.EndedAt);

        var second = session.Stop(Now.AddMinutes(6));
        Assert.True(second.IsT1);
    }

    [Fact]
    public void FocusSession_Stop_BeforeStart_Fails()
    {
        var session = FocusSession.Create(Now, "x").AsT0;
        var result = session.Stop(Now.AddMinutes(-1));
        Assert.True(result.IsT1);
    }

    [Fact]
    public void Label_Create_Valid_Succeeds_WithNullFeatures()
    {
        var result = Label.Create(Now, "https://a.com", "T", Decision.Block, LabelSource.GoodCall);

        Assert.True(result.IsT0);
        Assert.Null(result.AsT0.FeaturesJson);
    }

    [Fact]
    public void Label_Create_BlankUrl_Fails()
    {
        var result = Label.Create(Now, "", null, Decision.Allow, LabelSource.BadCall);
        Assert.True(result.IsT1);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void CategoryDomain_Create_OutOfRangeConfidence_Fails(double confidence)
    {
        var result = CategoryDomain.Create("news.com", "news", confidence, Now);
        Assert.True(result.IsT1);
    }

    [Fact]
    public void CategoryDomain_Create_Valid_Succeeds()
    {
        var result = CategoryDomain.Create("News.com", "news", 0.9, Now);

        Assert.True(result.IsT0);
        Assert.Equal("news.com", result.AsT0.Domain);
    }

    [Fact]
    public void DecisionLog_Create_BlankTier_Fails()
    {
        var result = DecisionLog.Create(Now, "https://a.com", "", "Block", 0.5);
        Assert.True(result.IsT1);
    }
}
