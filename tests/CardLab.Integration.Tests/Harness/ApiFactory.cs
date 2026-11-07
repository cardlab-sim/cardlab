using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace CardLab.Integration.Tests.Harness;

/// <summary>
/// In-process host on its own <see cref="SqlTestDatabase"/> (design §15.2): environment Development, settings via
/// <c>builder.UseSetting</c> (<c>ConnectionStrings:CardLab</c> = <see cref="Database"/>, <c>CardLab:TestEndpoints</c> =
/// <see cref="TestEndpointsEnabled"/>, <c>CardLab:Commit</c> = <c>"test-commit"</c>), and <see cref="Logs"/> registered as an
/// extra OpenTelemetry log processor (<c>services.AddOpenTelemetry().WithLogging(l =&gt; l.AddProcessor(new
/// SimpleLogRecordExportProcessor(Logs)))</c>). Use as <c>IClassFixture&lt;ApiFactory&gt;</c> in a <c>[Collection("sql")]</c> class.
/// PLANNER-DECISION: the factory owns its database (exposed as <see cref="Database"/>) because xunit class fixtures cannot
/// depend on each other.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly bool _ownsDatabase;

    /// <summary>Creates a host on a new database it owns (creates and drops).</summary>
    public ApiFactory()
        : this(new SqlTestDatabase(), ownsDatabase: true)
    {
    }

    /// <summary>Creates a host on a given database (002: restarts over the same data).</summary>
    /// <param name="database">The database.</param>
    /// <param name="ownsDatabase">True when this factory initialises and drops it.</param>
    protected ApiFactory(SqlTestDatabase database, bool ownsDatabase)
    {
        Database = database;
        _ownsDatabase = ownsDatabase;
    }

    /// <summary>The test database behind this host (arrange helpers, counts, dumps).</summary>
    public SqlTestDatabase Database { get; }

    /// <summary>Captured OpenTelemetry log records of this host.</summary>
    public InMemoryLogExporter Logs { get; } = new();

    /// <summary>Value of <c>CardLab:TestEndpoints</c> for this host; override to false for T-I-15.</summary>
    protected virtual bool TestEndpointsEnabled => true;

    /// <summary>002: <c>CardLab:Payments:BankTimeoutSeconds</c> for this host; null keeps the default (10 s).</summary>
    protected virtual int? BankTimeoutSeconds => null;

    /// <summary>
    /// 002 (AC-20, AC-22 restarts): a second host over the same database, with the same settings, as if the app restarted.
    /// It does not own the database; dispose it (<c>await using</c>) before this factory is disposed.
    /// </summary>
    /// <returns>A new, not yet started host.</returns>
    public ApiFactory CreateRestartedHost() => new RestartedApiFactory(Database, TestEndpointsEnabled, BankTimeoutSeconds);

    /// <summary>Creates the typed client.</summary>
    /// <returns>A client on this host.</returns>
    public ApiClient CreateApiClient() => new(CreateClient());

    /// <summary>Initialises the database, then the host.</summary>
    /// <returns>A task.</returns>
    public ValueTask InitializeAsync() => _ownsDatabase ? Database.InitializeAsync() : ValueTask.CompletedTask;

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (_ownsDatabase)
        {
            await Database.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:CardLab", Database.ConnectionString);
        builder.UseSetting("CardLab:TestEndpoints", TestEndpointsEnabled ? "true" : "false");
        builder.UseSetting("CardLab:Commit", "test-commit");
        if (BankTimeoutSeconds is { } seconds)
        {
            builder.UseSetting("CardLab:Payments:BankTimeoutSeconds", seconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        builder.ConfigureServices(services => services.AddOpenTelemetry().WithLogging(l => l.AddProcessor(new SimpleLogRecordExportProcessor(Logs))));
    }
}

/// <summary>A host with <c>CardLab:TestEndpoints=false</c> (T-I-15: reset must be 404). Complete in PR-00.</summary>
public sealed class ApiFactoryWithoutTestEndpoints : ApiFactory
{
    /// <inheritdoc />
    protected override bool TestEndpointsEnabled => false;
}

/// <summary>002: a host with <c>CardLab:Payments:BankTimeoutSeconds=1</c> for timeout paths (design §11, §15.2). Complete in PR-00.</summary>
public sealed class ApiFactoryWithShortBankTimeout : ApiFactory
{
    /// <inheritdoc />
    protected override int? BankTimeoutSeconds => 1;
}

/// <summary>002: a restarted host over an existing database (see <see cref="ApiFactory.CreateRestartedHost"/>).</summary>
internal sealed class RestartedApiFactory : ApiFactory
{
    private readonly bool _testEndpoints;
    private readonly int? _bankTimeoutSeconds;

    /// <summary>Creates the host.</summary>
    /// <param name="database">The shared database (not owned).</param>
    /// <param name="testEndpoints">Same TestEndpoints value as the original.</param>
    /// <param name="bankTimeoutSeconds">Same bank timeout as the original.</param>
    public RestartedApiFactory(SqlTestDatabase database, bool testEndpoints, int? bankTimeoutSeconds)
        : base(database, ownsDatabase: false)
    {
        _testEndpoints = testEndpoints;
        _bankTimeoutSeconds = bankTimeoutSeconds;
    }

    /// <inheritdoc />
    protected override bool TestEndpointsEnabled => _testEndpoints;

    /// <inheritdoc />
    protected override int? BankTimeoutSeconds => _bankTimeoutSeconds;
}
