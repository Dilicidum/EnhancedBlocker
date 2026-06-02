using EnhancedBlocker.Domain;

namespace EnhancedBlocker.Api.Contracts;

// ---- Events ----

public sealed record EventDto(
    DateTimeOffset? Ts,
    string Url,
    string Domain,
    string? Title,
    int TabId,
    EventType Type,
    Guid? FocusSessionId,
    long? DurationMs);

// ---- Decision ----

public sealed record DecisionRequest(
    string Url,
    string? Domain,
    string? Title,
    string? Text,
    Guid? FocusSessionId,
    string? Intent,
    DateTimeOffset? Now);

public sealed record DecisionResponse(string Outcome, string Tier, string Reason, double? Score);

// ---- Feedback ----

public sealed record FeedbackRequest(
    string Url,
    string? Title,
    Decision Decision,
    LabelSource? Source,
    DateTimeOffset? Ts);

// ---- Rules ----

public sealed record RuleRequest(
    string Pattern,
    MatchKind Match,
    RuleKind Kind,
    RuleSource? Source,
    string? Category);

public sealed record RuleResponse(
    Guid Id,
    string Pattern,
    MatchKind Match,
    RuleKind Kind,
    RuleSource Source,
    string? Category)
{
    public static RuleResponse From(Rule r) =>
        new(r.Id, r.Pattern, r.Match, r.Kind, r.Source, r.Category);
}

// ---- Focus ----

public sealed record StartFocusRequest(string Intent);

public sealed record StartFocusResponse(Guid FocusSessionId);

public sealed record StopFocusRequest(Guid? FocusSessionId);

public sealed record StopFocusResponse(Guid FocusSessionId);
