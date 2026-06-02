using OneOf;
using EnhancedBlocker.Domain;

namespace EnhancedBlocker.Application.Decisions;

/// <summary>
/// Everything a tier needs to decide on a page. <c>Text</c>, <c>Intent</c> and the focus fields
/// are M2 inputs but captured from day one so adding ML is additive.
/// </summary>
public sealed record DecisionContext(
    string Url,
    string Domain,
    string? Title,
    string? Text,
    Guid? FocusSessionId,
    string? Intent,
    DateTimeOffset Now);

/// <summary>A decisive verdict from a tier (or the cascade default).</summary>
public sealed record TierResult(Outcome Outcome, string Tier, string Reason, double? Score);

/// <summary>A tier abstaining: "I have no opinion, fall through to the next tier."</summary>
public sealed record Defer;
