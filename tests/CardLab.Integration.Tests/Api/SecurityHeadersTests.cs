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
    [Fact(Skip = "todo")]
    public void Security_headers_are_set_on_every_response()
    {
    }
}
