using System.Net;
using CardLab.Integration.Tests.Harness;

namespace CardLab.Integration.Tests.Api;

[Collection("sql")]
public sealed class SecurityHeadersTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public SecurityHeadersTests(ApiFactory factory) => _factory = factory;

    // T-I-20 · PR-09 · integration
    // Given the host, When GET /api/health, GET /api/cards and GET /cards/x (SPA fallback), Then each response has X-Content-Type-Options nosniff, Referrer-Policy, X-Frame-Options DENY, Content-Security-Policy with default-src 'self' and frame-ancestors 'none'; /api/* responses have Cache-Control no-store; GET /api/nope → 404 ProblemDetails (not index.html)
    // Catches: ZAP baseline failures (design §10, DEC-18), API 404s served as HTML
    [Fact]
    public async Task Security_headers_are_set_on_every_response()
    {
        using var client = _factory.CreateClient();

        foreach (var path in new[] { "/api/health", "/api/cards", "/api/nope", "/cards/x" })
        {
            using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

            Assert.Equal("nosniff", Header(response, "X-Content-Type-Options", path));
            Assert.Equal("no-referrer", Header(response, "Referrer-Policy", path));
            Assert.Equal("DENY", Header(response, "X-Frame-Options", path));
            Assert.Equal("same-origin", Header(response, "Cross-Origin-Opener-Policy", path));
            Assert.Equal("same-origin", Header(response, "Cross-Origin-Resource-Policy", path));
            Assert.Contains("camera=()", Header(response, "Permissions-Policy", path));
            var csp = Header(response, "Content-Security-Policy", path);
            Assert.Contains("default-src 'self'", csp);
            Assert.Contains("frame-ancestors 'none'", csp);
            Assert.DoesNotContain("unsafe-inline", csp);
            if (path.StartsWith("/api/", StringComparison.Ordinal))
            {
                Assert.Equal("no-store", Header(response, "Cache-Control", path));
            }
        }

        using var missing = await client.GetAsync("/api/nope", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("application/problem+json", missing.Content.Headers.ContentType?.MediaType);
    }

    private static string Header(HttpResponseMessage response, string name, string path) =>
        response.Headers.TryGetValues(name, out var values)
            ? string.Join(", ", values)
            : throw new Xunit.Sdk.XunitException($"{path} response has no {name} header.");
}
