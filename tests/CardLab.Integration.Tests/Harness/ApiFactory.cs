using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CardLab.Integration.Tests.Harness;

/// <summary>
/// In-process host on its own <see cref="SqlTestDatabase"/> (design §15.2): environment Development, settings via
/// <c>builder.UseSetting</c> (<c>ConnectionStrings:CardLab</c> = <see cref="Database"/>, <c>CardLab:TestEndpoints</c> =
/// <see cref="TestEndpointsEnabled"/>, <c>CardLab:Commit</c> = <c>"test-commit"</c>), and <see cref="Logs"/> registered as an
/// extra OpenTelemetry log processor (<c>services.AddOpenTelemetry().WithLogging(l =&gt; l.AddProcessor(new
/// SimpleLogRecordExportProcessor(Logs)))</c>). Use as <c>IClassFixture&lt;ApiFactory&gt;</c> in a <c>[Collection("sql")]</c> class.
/// PLANNER-DECISION: the factory owns its database (exposed as <see cref="Database"/>) because xunit class fixtures cannot
/// depend on each other. Implemented in PR-06.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>The test database behind this host (arrange helpers, counts, dumps).</summary>
    public SqlTestDatabase Database => throw new NotImplementedException("PR-06");

    /// <summary>Captured OpenTelemetry log records of this host.</summary>
    public InMemoryLogExporter Logs => throw new NotImplementedException("PR-06");

    /// <summary>Value of <c>CardLab:TestEndpoints</c> for this host; override to false for T-I-15.</summary>
    protected virtual bool TestEndpointsEnabled => true;

    /// <summary>Creates the typed client.</summary>
    /// <returns>A client on this host.</returns>
    public ApiClient CreateApiClient() => throw new NotImplementedException("PR-06");

    /// <summary>Initialises the database, then the host.</summary>
    /// <returns>A task.</returns>
    public ValueTask InitializeAsync() => throw new NotImplementedException("PR-06");

    /// <inheritdoc />
    public override ValueTask DisposeAsync() => throw new NotImplementedException("PR-06");

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder) => throw new NotImplementedException("PR-06");
}

/// <summary>A host with <c>CardLab:TestEndpoints=false</c> (T-I-15: reset must be 404). Complete in PR-00.</summary>
public sealed class ApiFactoryWithoutTestEndpoints : ApiFactory
{
    /// <inheritdoc />
    protected override bool TestEndpointsEnabled => false;
}
