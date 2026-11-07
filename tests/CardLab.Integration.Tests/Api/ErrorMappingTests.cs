using System.Runtime.CompilerServices;
using System.Text;
using CardLab.Api.Middleware;
using CardLab.Domain.Ports;
using CardLab.Integration.Tests.Harness;
using CardLab.TestKit;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CardLab.Integration.Tests.Api;

[Collection("sql")]
public sealed class BadRequestBodyTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public BadRequestBodyTests(ApiFactory factory) => _factory = factory;

    // T-I-5 · PR-09 review BUG-006
    // Given a numeric cardNumber body, When POST /api/authorisations, Then 400 application/problem+json, no 16-digit run in the response, nothing stored
    // Catches: unbindable bodies surfacing as 500 or echoing the card number (NFR-1)
    [Fact]
    public async Task Numeric_card_number_is_a_400_without_echo()
    {
        var api = _factory.CreateApiClient();
        var before = await _factory.Database.CountTransactionsAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/authorisations")
        {
            Content = new StringContent("{\"cardNumber\":9999000100000001,\"amount\":\"1.00\",\"category\":\"fuel\",\"merchant\":\"Shop\"}", Encoding.UTF8, "application/json"),
        };

        using var response = await api.SendRawAsync(request);
        var raw = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        PanScanner.AssertNoPan(raw, "400 body");
        Assert.Equal(before, await _factory.Database.CountTransactionsAsync());
    }
}

public sealed class ErrorMappingTests
{
    private const string Secret = "request-data-9999000100000001";

    // T-I-21 · PR-09 · integration (handler, no database)
    // Given CardLabExceptionHandler and a DefaultHttpContext with a ProblemDetails service, When TryHandleAsync with CardLockTimeoutException and with a connection-level SqlException/DbUpdateException, Then true and 503 application/problem+json ("Try again" / "Service unavailable") whose body does not contain the exception message; any other exception → false
    // Catches: lock timeouts surfacing as 500, or exception text (possibly with request data) in responses (design §9, NFR-1)
    [Fact]
    public async Task Infrastructure_failures_map_to_503()
    {
        var sql = SqlFailure();
        var cases = new (Exception Exception, string Title)[]
        {
            (new CardLockTimeoutException(Secret), "Try again"),
            (sql, "Service unavailable"),
            (new DbUpdateException(Secret, sql), "Service unavailable"),
            (new InvalidOperationException(Secret, sql), "Service unavailable"),
            (new BadHttpRequestException(Secret, StatusCodes.Status400BadRequest), "Bad request"),
        };

        foreach (var (exception, title) in cases)
        {
            var (handled, status, contentType, body) = await HandleAsync(exception);

            Assert.True(handled, exception.GetType().Name);
            Assert.Equal(exception is BadHttpRequestException ? 400 : 503, status);
            Assert.StartsWith("application/problem+json", contentType, StringComparison.Ordinal);
            Assert.Contains($"\"title\":\"{title}\"", body);
            Assert.DoesNotContain(Secret, body);
        }

        foreach (var other in new Exception[] { new InvalidOperationException(Secret), new DbUpdateException(Secret), new ArgumentException(Secret) })
        {
            var (handled, _, _, body) = await HandleAsync(other);

            Assert.False(handled, other.GetType().Name);
            Assert.Equal(string.Empty, body);
        }
    }

    private static SqlException SqlFailure() =>
        (SqlException)RuntimeHelpers.GetUninitializedObject(typeof(SqlException));

    private static async Task<(bool Handled, int Status, string? ContentType, string Body)> HandleAsync(Exception exception)
    {
        var services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        var handler = new CardLabExceptionHandler(services.GetRequiredService<IProblemDetailsService>());
        var context = new DefaultHttpContext { RequestServices = services };
        await using var stream = new MemoryStream();
        context.Response.Body = stream;

        var handled = await handler.TryHandleAsync(context, exception, TestContext.Current.CancellationToken);

        return (handled, context.Response.StatusCode, context.Response.ContentType, Encoding.UTF8.GetString(stream.ToArray()));
    }
}
