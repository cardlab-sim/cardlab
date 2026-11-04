using Microsoft.AspNetCore.Diagnostics;

namespace CardLab.Api.Middleware;

/// <summary>Response security headers so the ZAP baseline passes (design §10).</summary>
public static class SecurityHeaders
{
    /// <summary>
    /// Adds to every response: <c>X-Content-Type-Options: nosniff</c>, <c>Referrer-Policy: no-referrer</c>,
    /// <c>X-Frame-Options: DENY</c>, <c>Permissions-Policy</c> (camera, microphone, geolocation disabled),
    /// <c>Cross-Origin-Opener-Policy: same-origin</c>, <c>Cross-Origin-Resource-Policy: same-origin</c> and
    /// <c>Content-Security-Policy: default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:;
    /// font-src 'self'; connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self';
    /// frame-ancestors 'none'</c>; <c>Cache-Control: no-store</c> on <c>/api/*</c>. HSTS comes from <c>UseHsts</c>.
    /// </summary>
    /// <remarks>PLANNER-DECISION: in the skeleton this is a pass-through (not a throwing stub) because it runs on
    /// every request and parallel PRs need a working host. Implemented in PR-09.</remarks>
    /// <param name="app">Application builder.</param>
    /// <returns>The same builder.</returns>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) => app;
}

/// <summary>
/// Maps infrastructure failures to 503 ProblemDetails (design §9): <c>CardLockTimeoutException</c> ("Try again") and
/// <c>Microsoft.Data.SqlClient.SqlException</c> / EF connection failures ("Service unavailable"). Never includes request
/// bodies or exception messages that could carry a card number.
/// </summary>
/// <remarks>PLANNER-DECISION: skeleton returns false (default 500 handling) instead of throwing, so the pipeline stays
/// usable for parallel PRs. Implemented in PR-09.</remarks>
public sealed class CardLabExceptionHandler : IExceptionHandler
{
    /// <inheritdoc />
    public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken) => ValueTask.FromResult(false);
}
