namespace CardLab.Integration.Tests.Api;

public sealed class ErrorMappingTests
{
    // T-I-21 · PR-09 · integration (handler, no database)
    // Given CardLabExceptionHandler and a DefaultHttpContext with a ProblemDetails service, When TryHandleAsync with CardLockTimeoutException and with a connection-level SqlException/DbUpdateException, Then true and 503 application/problem+json ("Try again" / "Service unavailable") whose body does not contain the exception message; any other exception → false
    // Catches: lock timeouts surfacing as 500, or exception text (possibly with request data) in responses (design §9, NFR-1)
    [Fact(Skip = "todo")]
    public void Infrastructure_failures_map_to_503()
    {
    }
}
