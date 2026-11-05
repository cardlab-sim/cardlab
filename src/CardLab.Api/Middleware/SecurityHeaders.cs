using CardLab.Domain.Ports;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CardLab.Api.Middleware;

/// <summary>Response security headers so the ZAP baseline passes (design §10).</summary>
public static class SecurityHeaders
{
    /// <summary>
    /// Adds to every response: <c>X-Content-Type-Options: nosniff</c>, <c>Referrer-Policy: no-referrer</c>,
    /// <c>X-Frame-Options: DENY</c>, <c>Permissions-Policy</c> (camera, microphone, geolocation disabled),
    /// <c>Cross-Origin-Opener-Policy: same-origin</c>, <c>Cross-Origin-Resource-Policy: same-origin</c>, <c>Cross-Origin-Embedder-Policy: require-corp</c> and
    /// <c>Content-Security-Policy: default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:;
    /// font-src 'self'; connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self';
    /// frame-ancestors 'none'</c>; <c>Cache-Control: no-store</c> on <c>/api/*</c>. HSTS comes from <c>UseHsts</c>.
    /// </summary>
    /// <remarks>Registered through <c>Response.OnStarting</c> so responses produced after exception handling are covered too.</remarks>
    /// <param name="app">Application builder.</param>
    /// <returns>The same builder.</returns>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            // OnStarting runs when the response begins, so static files, 404s and exception-handler output all carry the headers.
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers["X-Content-Type-Options"] = "nosniff";
                headers["Referrer-Policy"] = "no-referrer";
                headers["X-Frame-Options"] = "DENY";
                headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
                headers["Cross-Origin-Opener-Policy"] = "same-origin";
                headers["Cross-Origin-Resource-Policy"] = "same-origin";
                headers["Cross-Origin-Embedder-Policy"] = "require-corp";
                headers["Content-Security-Policy"] = ContentSecurityPolicy;
                if (context.Request.Path.StartsWithSegments("/api"))
                {
                    headers["Cache-Control"] = "no-store";
                }

                return Task.CompletedTask;
            });
            return next(context);
        });

    private const string ContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; font-src 'self'; " +
        "connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";
}

/// <summary>
/// Maps infrastructure failures to 503 ProblemDetails (design §9): <c>CardLockTimeoutException</c> ("Try again") and
/// <c>Microsoft.Data.SqlClient.SqlException</c> / EF connection failures ("Service unavailable"). Never includes request
/// bodies or exception messages that could carry a card number.
/// </summary>
/// <remarks>Any other exception returns false so the default 500 handling applies. Needs <c>AddProblemDetails</c>.</remarks>
public sealed class CardLabExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        string title;
        if (exception is CardLockTimeoutException)
        {
            title = "Try again";
        }
        else if (IsSqlFailure(exception))
        {
            title = "Service unavailable";
        }
        else
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = { Status = StatusCodes.Status503ServiceUnavailable, Title = title },
        });
    }

    private static bool IsSqlFailure(Exception exception) =>
        exception is SqlException
        || (exception is DbUpdateException or InvalidOperationException && exception.InnerException is SqlException);
}
