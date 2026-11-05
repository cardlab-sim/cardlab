using CardLab.Api.Endpoints;
using CardLab.Api.Middleware;
using CardLab.Infrastructure;
using CardLab.Infrastructure.Logging;
using CardLab.Infrastructure.Sql;

// Host wiring (shared; complete in PR-00, later PRs never edit this file). Design §4, DEC-3, DEC-19.
var builder = WebApplication.CreateBuilder(args);
builder.AddCardLabTelemetry();
builder.AddCardLabInfrastructure();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<CardLabExceptionHandler>();
builder.Services.AddOpenApi();
// Unbindable bodies throw BadHttpRequestException so CardLabExceptionHandler answers a ProblemDetails 400 in every environment.
builder.Services.Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = true);

var app = builder.Build();

app.UseExceptionHandler();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseSecurityHeaders();
app.UseDefaultFiles();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapCardEndpoints();
app.MapAuthorisationEndpoints();
app.MapHealthEndpoint();
if (app.Configuration.GetValue<bool>("CardLab:TestEndpoints"))
{
    app.MapTestEndpoints();
}

// Unknown /api/* routes are 404 ProblemDetails, never the SPA's index.html.
app.Map("/api/{**path}", () => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound));
app.MapFallbackToFile("index.html");

await app.Services.InitialiseDatabaseAsync();
await app.RunAsync();

/// <summary>Entry point; public partial so <c>WebApplicationFactory&lt;Program&gt;</c> can host it in tests.</summary>
public partial class Program;
