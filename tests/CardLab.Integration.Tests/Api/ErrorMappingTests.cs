using System.Runtime.CompilerServices;
using System.Text;
using CardLab.Api.Middleware;
using CardLab.Domain.Ports;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CardLab.Integration.Tests.Api;

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
        };

        foreach (var (exception, title) in cases)
        {
            var (handled, status, contentType, body) = await HandleAsync(exception);

            Assert.True(handled, exception.GetType().Name);
            Assert.Equal(503, status);
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
