using EnhancedBlocker.Api.Contracts;
using EnhancedBlocker.Application.Decisions;
using EnhancedBlocker.Application.Events;
using EnhancedBlocker.Application.Feedback;
using EnhancedBlocker.Application.Focus;
using EnhancedBlocker.Application.Messaging;
using EnhancedBlocker.Application.Rules;
using EnhancedBlocker.Domain;

namespace EnhancedBlocker.Api.Endpoints;

/// <summary>
/// Thin Minimal-API endpoints: each builds a CQRS message, sends it, and maps the
/// <c>OneOf</c> result to an HTTP response. No domain logic lives here.
/// </summary>
public static class ApiEndpoints
{
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

        app.MapPost("/events", async (EventDto[] body, ISender sender, CancellationToken ct) =>
        {
            var inputs = body.Select(e => new LogEventInput(
                e.Ts ?? DateTimeOffset.UtcNow,
                e.Url,
                string.IsNullOrWhiteSpace(e.Domain) ? UrlHelper.DomainFromUrl(e.Url) : e.Domain,
                e.Title,
                e.TabId,
                e.Type,
                e.FocusSessionId,
                e.DurationMs)).ToList();

            var result = await sender.Send(new LogEventsCommand(inputs), ct);
            return result.Match(
                count => Results.Accepted(value: new { logged = count }),
                BadRequest);
        });

        app.MapPost("/decision", async (DecisionRequest body, ISender sender, CancellationToken ct) =>
        {
            var ctx = new DecisionContext(
                body.Url,
                string.IsNullOrWhiteSpace(body.Domain) ? UrlHelper.DomainFromUrl(body.Url) : body.Domain!,
                body.Title,
                body.Text,
                body.FocusSessionId,
                body.Intent,
                body.Now ?? DateTimeOffset.UtcNow);

            var result = await sender.Send(new DecideQuery(ctx), ct);
            return result.Match(
                tr => Results.Ok(new DecisionResponse(tr.Outcome.ToString(), tr.Tier, tr.Reason, tr.Score)),
                BadRequest);
        });

        app.MapPost("/feedback", async (FeedbackRequest body, ISender sender, CancellationToken ct) =>
        {
            var command = new RecordFeedbackCommand(
                body.Url,
                body.Title,
                body.Decision,
                body.Source ?? (body.Decision == Decision.Block ? LabelSource.GoodCall : LabelSource.BadCall),
                body.Ts ?? DateTimeOffset.UtcNow);

            var result = await sender.Send(command, ct);
            return result.Match(
                id => Results.Created($"/feedback/{id}", new { id }),
                BadRequest);
        });

        app.MapGet("/rules", async (ISender sender, CancellationToken ct) =>
        {
            var rules = await sender.Send(new ListRulesQuery(), ct);
            return Results.Ok(rules.Select(RuleResponse.From));
        });

        app.MapPost("/rules", async (RuleRequest body, ISender sender, CancellationToken ct) =>
        {
            var command = new AddRuleCommand(
                body.Pattern,
                body.Match,
                body.Kind,
                body.Source ?? RuleSource.Manual,
                body.Category);

            var result = await sender.Send(command, ct);
            return result.Match(
                rule => Results.Created($"/rules/{rule.Id}", RuleResponse.From(rule)),
                BadRequest);
        });

        app.MapDelete("/rules/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var deleted = await sender.Send(new DeleteRuleCommand(id), ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        app.MapPost("/focus/start", async (StartFocusRequest body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new StartFocusCommand(body.Intent, DateTimeOffset.UtcNow), ct);
            return result.Match(
                id => Results.Ok(new StartFocusResponse(id)),
                BadRequest);
        });

        app.MapPost("/focus/stop", async (StopFocusRequest? body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new StopFocusCommand(body?.FocusSessionId, DateTimeOffset.UtcNow), ct);
            return result.Match(
                id => Results.Ok(new StopFocusResponse(id)),
                BadRequest);
        });

        return app;
    }

    private static IResult BadRequest(ValidationError error) =>
        Results.BadRequest(new { error = error.Message });
}
