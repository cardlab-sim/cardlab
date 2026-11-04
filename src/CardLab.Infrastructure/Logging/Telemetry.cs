using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

namespace CardLab.Infrastructure.Logging;

/// <summary>OpenTelemetry logging wiring (DEC-12). Complete in PR-00 (shared startup wiring).</summary>
public static class Telemetry
{
    /// <summary>
    /// Adds OpenTelemetry logging with scopes and formatted messages, resource <c>service.name=cardlab</c>,
    /// <c>service.version=&lt;CardLab:Commit&gt;</c>, a console exporter, and an OTLP exporter only when
    /// <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is configured. Tests add their own exporter on top (InMemoryLogExporter).
    /// </summary>
    /// <param name="builder">The host builder.</param>
    /// <returns>The same builder.</returns>
    public static IHostApplicationBuilder AddCardLabTelemetry(this IHostApplicationBuilder builder)
    {
        var commit = builder.Configuration["CardLab:Commit"];
        var otlp = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("cardlab", serviceVersion: string.IsNullOrEmpty(commit) ? "unknown" : commit))
            .WithLogging(
                logging =>
                {
                    logging.AddConsoleExporter();
                    if (!string.IsNullOrEmpty(otlp))
                    {
                        logging.AddOtlpExporter();
                    }
                },
                options =>
                {
                    options.IncludeScopes = true;
                    options.IncludeFormattedMessage = true;
                });
        return builder;
    }
}
