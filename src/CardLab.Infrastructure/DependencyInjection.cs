using CardLab.Domain.Application;
using CardLab.Domain.Ports;
using CardLab.Infrastructure.Identity;
using CardLab.Infrastructure.Lookup;
using CardLab.Infrastructure.Model;
using CardLab.Infrastructure.Options;
using CardLab.Infrastructure.Sql;
using CardLab.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CardLab.Infrastructure;

/// <summary>All service registrations of the host (shared wiring, complete in PR-00; later PRs never edit it).</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers options, the DbContext (SQL Server, connection string <c>ConnectionStrings:CardLab</c> read lazily so
    /// test hosts can override it), the ports' real adapters and the application services.
    /// </summary>
    /// <param name="builder">The host builder.</param>
    /// <returns>The same builder.</returns>
    public static IHostApplicationBuilder AddCardLabInfrastructure(this IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.Configure<CardLabOptions>(builder.Configuration.GetSection(CardLabOptions.Section));
        services.Configure<LookupOptions>(builder.Configuration.GetSection(LookupOptions.Section));
        services.Configure<SeedOptions>(builder.Configuration.GetSection(SeedOptions.Section));

        services.AddDbContext<CardLabDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("CardLab")
                ?? throw new InvalidOperationException("ConnectionStrings:CardLab is not configured.");
            options.UseSqlServer(connectionString);
        });

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<ICardNumberLookup, HmacCardNumberLookup>();
        services.AddSingleton<ICurrentActor, SeededAdminActor>();
        services.AddScoped<ICardStore, SqlCardStore>();
        services.AddScoped<DatabaseProbe>();
        services.AddScoped<TestDataReset>();
        services.AddScoped<AuthorisationService>();
        services.AddScoped<CardControlsService>();
        return builder;
    }
}
