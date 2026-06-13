using EnhancedBlocker.Api.Contracts;
using EnhancedBlocker.Application.Categories;
using EnhancedBlocker.Application.Decisions;
using EnhancedBlocker.Application.Events;
using EnhancedBlocker.Application.Feedback;
using EnhancedBlocker.Application.Focus;
using EnhancedBlocker.Application.Rules;
using EnhancedBlocker.Domain;

namespace EnhancedBlocker.Api.Endpoints;

/// <summary>
/// Thin Minimal-API endpoints: each builds a CQRS message, invokes the relevant handler
/// directly, and maps the <c>OneOf</c> result to an HTTP response. No domain logic lives here.
/// </summary>
public static class ApiEndpoints
{
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

        app.MapPost("/events", async (EventDto[] body, LogEventsCommandHandler handler, CancellationToken ct) =>
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

            var result = await handler.Handle(new LogEventsCommand(inputs), ct);
            return result.Match(
                count => Results.Accepted(value: new { logged = count }),
                BadRequest);
        });

        app.MapPost("/decision", async (DecisionRequest body, DecideQueryHandler handler, CancellationToken ct) =>
        {
            var ctx = new DecisionContext(
                body.Url,
                string.IsNullOrWhiteSpace(body.Domain) ? UrlHelper.DomainFromUrl(body.Url) : body.Domain!,
                body.Title,
                body.Text,
                body.FocusSessionId,
                body.Intent,
                body.Now ?? DateTimeOffset.UtcNow);

            var result = await handler.Handle(new DecideQuery(ctx), ct);
            return result.Match(
                tr => Results.Ok(new DecisionResponse(tr.Outcome.ToString(), tr.Tier, tr.Reason, tr.Score)),
                BadRequest);
        });

        app.MapPost("/feedback", async (FeedbackRequest body, RecordFeedbackCommandHandler handler, CancellationToken ct) =>
        {
            var command = new RecordFeedbackCommand(
                body.Url,
                body.Title,
                body.Decision,
                body.Source ?? (body.Decision == Decision.Block ? LabelSource.GoodCall : LabelSource.BadCall),
                body.Ts ?? DateTimeOffset.UtcNow);

            var result = await handler.Handle(command, ct);
            return result.Match(
                id => Results.Created($"/feedback/{id}", new { id }),
                BadRequest);
        });

        app.MapGet("/rules", async (ListRulesQueryHandler handler, CancellationToken ct) =>
        {
            var rules = await handler.Handle(new ListRulesQuery(), ct);
            return Results.Ok(rules.Select(RuleResponse.From));
        });

        app.MapPost("/rules", async (RuleRequest body, AddRuleCommandHandler handler, CancellationToken ct) =>
        {
            var command = new AddRuleCommand(
                body.Pattern,
                body.Match,
                body.Kind,
                body.Source ?? RuleSource.Manual,
                body.Category);

            var result = await handler.Handle(command, ct);
            return result.Match(
                rule => Results.Created($"/rules/{rule.Id}", RuleResponse.From(rule)),
                BadRequest);
        });

        app.MapDelete("/rules/{id:guid}", async (Guid id, DeleteRuleCommandHandler handler, CancellationToken ct) =>
        {
            var deleted = await handler.Handle(new DeleteRuleCommand(id), ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        app.MapPost("/focus/start", async (StartFocusRequest body, StartFocusCommandHandler handler, CancellationToken ct) =>
        {
            var result = await handler.Handle(new StartFocusCommand(body.Intent, DateTimeOffset.UtcNow), ct);
            return result.Match(
                id => Results.Ok(new StartFocusResponse(id)),
                BadRequest);
        });

        app.MapPost("/focus/stop", async (StopFocusRequest? body, StopFocusCommandHandler handler, CancellationToken ct) =>
        {
            var result = await handler.Handle(new StopFocusCommand(body?.FocusSessionId, DateTimeOffset.UtcNow), ct);
            return result.Match(
                id => Results.Ok(new StopFocusResponse(id)),
                BadRequest);
        });

        // ---- Categories (managed vocabulary; CRUD from the settings page) ----

        app.MapGet("/categories", async (ListCategoriesQueryHandler handler, CancellationToken ct) =>
        {
            var categories = await handler.Handle(new ListCategoriesQuery(), ct);
            return Results.Ok(categories.Select(CategoryResponse.From));
        });

        app.MapPost("/categories", async (CategoryRequest body, AddCategoryCommandHandler handler, CancellationToken ct) =>
        {
            var result = await handler.Handle(new AddCategoryCommand(body.Name), ct);
            return result.Match(
                category => Results.Created($"/categories/{category.Id}", CategoryResponse.From(category)),
                BadRequest);
        });

        app.MapPut("/categories/{id:guid}", async (Guid id, CategoryRequest body, UpdateCategoryCommandHandler handler, CancellationToken ct) =>
        {
            var result = await handler.Handle(new UpdateCategoryCommand(id, body.Name), ct);
            return result.Match(
                category => Results.Ok(CategoryResponse.From(category)),
                BadRequest);
        });

        app.MapDelete("/categories/{id:guid}", async (Guid id, DeleteCategoryCommandHandler handler, CancellationToken ct) =>
        {
            var deleted = await handler.Handle(new DeleteCategoryCommand(id), ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        return app;
    }

    private static IResult BadRequest(ValidationError error) =>
        Results.BadRequest(new { error = error.Message });
}
