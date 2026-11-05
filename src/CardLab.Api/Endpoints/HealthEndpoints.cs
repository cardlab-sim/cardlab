using System.Reflection;
using CardLab.Api.Contracts;
using CardLab.Infrastructure.Options;
using CardLab.Infrastructure.Sql;
using Microsoft.Extensions.Options;

namespace CardLab.Api.Endpoints;

/// <summary><c>GET /api/health</c> (R-14, design §12).</summary>
/// <remarks>Route complete in PR-00; handler implemented in PR-09.</remarks>
public static class HealthEndpoints
{
    /// <summary>Maps the health route.</summary>
    /// <param name="app">Route builder.</param>
    /// <returns>The same builder.</returns>
    public static IEndpointRouteBuilder MapHealthEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/health", Health);
        return app;
    }

    /// <summary>Health commit: <c>CardLab:Commit</c>; else the source-revision part (after the last '+') of the informational version; else "unknown".</summary>
    /// <param name="configured">The configured commit, possibly empty.</param>
    /// <param name="informationalVersion">The entry assembly's informational version, e.g. <c>1.0.0+&lt;sha&gt;</c>.</param>
    /// <returns>The commit to report.</returns>
    public static string ResolveCommit(string? configured, string? informationalVersion)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        if (string.IsNullOrWhiteSpace(informationalVersion))
        {
            return "unknown";
        }

        var plus = informationalVersion.LastIndexOf('+');
        return plus >= 0 && plus < informationalVersion.Length - 1 ? informationalVersion[(plus + 1)..] : informationalVersion;
    }

    /// <summary>
    /// 200 <see cref="HealthDto"/> <c>{commit, database:"ok"}</c> when <see cref="DatabaseProbe.CanConnectAsync"/> succeeds,
    /// else 503 <c>{commit, database:"unreachable"}</c>. Commit = <c>CardLab:Commit</c>, falling back to the source
    /// revision (the part after '+') of the entry assembly's informational version when empty.
    /// </summary>
    internal static async Task<IResult> Health(DatabaseProbe probe, IOptions<CardLabOptions> options, CancellationToken ct)
    {
        var commit = ResolveCommit(
            options.Value.Commit,
            Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);
        return await probe.CanConnectAsync(ct)
            ? TypedResults.Ok(new HealthDto(commit, "ok"))
            : TypedResults.Json(new HealthDto(commit, "unreachable"), statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}

/// <summary><c>POST /api/test/reset</c> (DEC-16), mapped by Program.cs only when <c>CardLab:TestEndpoints=true</c>.</summary>
/// <remarks>Route complete in PR-00; handler implemented in PR-09.</remarks>
public static class TestEndpoints
{
    /// <summary>Maps the test routes.</summary>
    /// <param name="app">Route builder.</param>
    /// <returns>The same builder.</returns>
    public static IEndpointRouteBuilder MapTestEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/test/reset", Reset);
        return app;
    }

    /// <summary>Runs <see cref="TestDataReset.ResetAsync"/> → 204.</summary>
    internal static async Task<IResult> Reset(TestDataReset reset, CancellationToken ct)
    {
        await reset.ResetAsync(ct);
        return TypedResults.NoContent();
    }
}
