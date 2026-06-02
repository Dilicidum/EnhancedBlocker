using OneOf;
using EnhancedBlocker.Application.Decisions;
using EnhancedBlocker.Application.Ports;
using EnhancedBlocker.Domain;

namespace EnhancedBlocker.Infrastructure.Decisions;

/// <summary>
/// Tier 0 of the cascade: deterministic hard rules.
/// Matches the request against <see cref="Rule"/>s (Exact URL or Domain) and the
/// <see cref="CategoryDomain"/> cache. Returns a decisive <see cref="TierResult"/> or
/// <see cref="Defer"/> when nothing matches.
/// <para>
/// Precedence: an explicit Allow rule wins over a Block rule (an allowlist entry is an
/// intentional override), so "sites I always permit" can never be blocked by Tier 0.
/// </para>
/// </summary>
public sealed class Tier0RuleTier(IRuleRepository rules, ICategoryDomainCache categories) : IDecisionTier
{
    public const string TierName = "tier0";

    public int Order => 0;

    public async Task<OneOf<TierResult, Defer>> EvaluateAsync(DecisionContext ctx, CancellationToken ct)
    {
        var url = ctx.Url.Trim();
        var domain = ctx.Domain.Trim().ToLowerInvariant();

        var allRules = await rules.ListAsync(ct);

        var blockMatch = (Rule?)null;
        foreach (var rule in allRules)
        {
            if (!Matches(rule, url, domain))
                continue;

            // An Allow rule short-circuits immediately (highest precedence).
            if (rule.Kind == RuleKind.Allow)
                return new TierResult(Outcome.Allow, TierName, $"allow rule: {rule.Pattern}", null);

            blockMatch ??= rule;
        }

        if (blockMatch is not null)
            return new TierResult(Outcome.Block, TierName, $"block rule: {blockMatch.Pattern}", null);

        // Category cache: a domain present here was promoted as an always-block category.
        var category = await categories.GetAsync(domain, ct);
        if (category is not null)
        {
            return new TierResult(
                Outcome.Block,
                TierName,
                $"category: {category.Category}",
                category.Confidence);
        }

        return new Defer();
    }

    private static bool Matches(Rule rule, string url, string domain) => rule.Match switch
    {
        // Exact: full URL equality (case-insensitive on the URL string).
        MatchKind.Exact => string.Equals(rule.Pattern, url, StringComparison.OrdinalIgnoreCase),
        // Domain: the request domain equals the pattern or is a subdomain of it.
        MatchKind.Domain => DomainMatches(rule.Pattern, domain),
        _ => false
    };

    private static bool DomainMatches(string pattern, string domain) =>
        domain == pattern || domain.EndsWith("." + pattern, StringComparison.Ordinal);
}
