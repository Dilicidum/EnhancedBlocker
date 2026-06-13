using System.Net;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using EnhancedBlocker.Application.Ports;
using EnhancedBlocker.Domain;
using EnhancedBlocker.Tests.Fakes;

namespace EnhancedBlocker.Tests.Api;

/// <summary>
/// Boots the real host (TestServer) with repositories swapped for in-memory fakes — no
/// database needed, AutoMigrate off.
/// </summary>
public sealed class ApiTestFactory : WebApplicationFactory<Program>
{
    public const string Token = "test-token";

    public FakeEventStore Events { get; } = new();
    public FakeLabelStore Labels { get; } = new();
    public FakeRuleRepository Rules { get; } = new();
    public FakeCategoryDomainCache Categories { get; } = new();
    public FakeFocusSessionRepository Sessions { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("EnhancedBlocker:ApiToken", Token);
        builder.UseSetting("EnhancedBlocker:AutoMigrate", "false");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEventStore>();
            services.RemoveAll<ILabelStore>();
            services.RemoveAll<IRuleRepository>();
            services.RemoveAll<ICategoryDomainCache>();
            services.RemoveAll<IFocusSessionRepository>();

            services.AddSingleton<IEventStore>(Events);
            services.AddSingleton<ILabelStore>(Labels);
            services.AddSingleton<IRuleRepository>(Rules);
            services.AddSingleton<ICategoryDomainCache>(Categories);
            services.AddSingleton<IFocusSessionRepository>(Sessions);
        });
    }
}

/// <summary>
/// Endpoint-level wire-contract tests. The request bodies are byte-for-byte what the
/// extension sends (camelCase, string enums — see extension/src/ui/app/core/models.ts),
/// pinning the JSON contract so a binding regression fails here instead of silently
/// 400-ing in the field.
/// </summary>
public sealed class ApiContractTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private HttpClient CreateClient(bool withToken = true)
    {
        var client = factory.CreateClient();
        if (withToken)
            client.DefaultRequestHeaders.Add("X-EB-Token", ApiTestFactory.Token);
        return client;
    }

    private static StringContent Json(string raw) => new(raw, Encoding.UTF8, "application/json");

    [Fact]
    public async Task Health_is_anonymous()
    {
        var res = await CreateClient(withToken: false).GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Requests_without_token_are_rejected()
    {
        var res = await CreateClient(withToken: false).GetAsync("/rules");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Events_bind_the_worker_payload()
    {
        // Exactly what background.ts logNavigation() sends.
        var res = await CreateClient().PostAsync("/events", Json("""
            [{
              "ts": "2026-06-13T10:00:00.000Z",
              "url": "https://example.com/article",
              "domain": "example.com",
              "title": null,
              "tabId": 7,
              "type": "navigate",
              "focusSessionId": null,
              "durationMs": null
            }]
            """));

        Assert.Equal(HttpStatusCode.Accepted, res.StatusCode);
        var evt = Assert.Single(factory.Events.Events, e => e.Url == "https://example.com/article");
        Assert.Equal(EventType.Navigate, evt.Type);
    }

    [Fact]
    public async Task Feedback_good_call_binds_and_writes_a_label()
    {
        // Exactly what the block screen's "Good call" sends.
        var res = await CreateClient().PostAsync("/feedback", Json("""
            {
              "url": "https://blocked.example/page",
              "title": "Blocked page",
              "decision": "block",
              "source": "GoodCall"
            }
            """));

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var label = Assert.Single(factory.Labels.Labels, l => l.Url == "https://blocked.example/page");
        Assert.Equal(Decision.Block, label.Decision);
        Assert.Equal(LabelSource.GoodCall, label.Source);
    }

    [Fact]
    public async Task Feedback_source_defaults_from_decision_when_omitted()
    {
        var res = await CreateClient().PostAsync("/feedback", Json("""
            { "url": "https://overridden.example/page", "decision": "allow" }
            """));

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var label = Assert.Single(factory.Labels.Labels, l => l.Url == "https://overridden.example/page");
        Assert.Equal(Decision.Allow, label.Decision);
        Assert.Equal(LabelSource.BadCall, label.Source);
    }

    [Fact]
    public async Task Rules_round_trip_as_string_enums()
    {
        // Exactly what the options page's "Add rule" sends.
        var client = CreateClient();
        var post = await client.PostAsync("/rules", Json("""
            {
              "pattern": "distracting.example",
              "match": "Domain",
              "kind": "Block",
              "source": "manual",
              "category": null
            }
            """));

        Assert.Equal(HttpStatusCode.Created, post.StatusCode);

        // The options page types match/kind as strings ('Exact'|'Domain') — not numbers.
        var list = await client.GetStringAsync("/rules");
        Assert.Contains("\"pattern\":\"distracting.example\"", list);
        Assert.Contains("\"match\":\"Domain\"", list);
        Assert.Contains("\"kind\":\"Block\"", list);
        Assert.DoesNotContain("\"match\":1", list);
    }

    [Fact]
    public async Task Decision_outcome_serializes_as_string()
    {
        var res = await CreateClient().PostAsync("/decision", Json("""
            { "url": "https://github.com/some/repo" }
            """));

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("\"outcome\":\"Allow\"", body);
    }
}
