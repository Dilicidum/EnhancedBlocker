using Microsoft.Extensions.Options;
using EnhancedBlocker.Api.Configuration;

namespace EnhancedBlocker.Api.Middleware;

/// <summary>
/// Shared-secret guard. Requires the configured token in the <c>X-EB-Token</c> header for every
/// request except <c>/health</c> (and CORS preflight <c>OPTIONS</c>). Good enough for a single-user
/// loopback API; documented as a thing to harden later.
/// </summary>
public sealed class TokenAuthMiddleware(RequestDelegate next, IOptions<EnhancedBlockerOptions> options)
{
    public const string HeaderName = "X-EB-Token";
    private readonly string _expectedToken = options.Value.ApiToken;

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;

        // /health bypasses auth; preflight requests carry no custom headers.
        if (HttpMethods.IsOptions(context.Request.Method) ||
            path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var provided = context.Request.Headers[HeaderName].ToString();
        if (string.IsNullOrEmpty(_expectedToken) || !FixedTimeEquals(provided, _expectedToken))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid or missing X-EB-Token." });
            return;
        }

        await next(context);
    }

    // Length-leaking but constant-time per-char comparison; adequate for a localhost shared secret.
    private static bool FixedTimeEquals(string a, string b)
    {
        if (a.Length != b.Length)
            return false;

        var diff = 0;
        for (var i = 0; i < a.Length; i++)
            diff |= a[i] ^ b[i];
        return diff == 0;
    }
}
