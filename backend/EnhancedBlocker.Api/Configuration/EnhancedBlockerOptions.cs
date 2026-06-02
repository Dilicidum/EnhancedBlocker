namespace EnhancedBlocker.Api.Configuration;

/// <summary>Backend configuration, bound from the "EnhancedBlocker" section.</summary>
public sealed class EnhancedBlockerOptions
{
    public const string SectionName = "EnhancedBlocker";

    /// <summary>Loopback port Kestrel binds to.</summary>
    public int Port { get; set; } = 5180;

    /// <summary>Shared secret required in the <c>X-EB-Token</c> header (except <c>/health</c>).</summary>
    public string ApiToken { get; set; } = string.Empty;

    /// <summary>Stable Chrome extension id, used to build the allowed CORS origin.</summary>
    public string ExtensionId { get; set; } = string.Empty;

    /// <summary>When true (dev only), CORS allows any origin to ease local testing.</summary>
    public bool AllowDevOrigins { get; set; }
}
