namespace EnhancedBlocker.Api.Contracts;

internal static class UrlHelper
{
    /// <summary>
    /// Extracts the host from a URL, falling back to the raw string if it won't parse.
    /// Returns empty for null/blank input so downstream domain validation produces a clean
    /// 400 (rather than an NRE → 500).
    /// </summary>
    public static string DomainFromUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host))
            return uri.Host.ToLowerInvariant();

        return url.Trim().ToLowerInvariant();
    }
}
