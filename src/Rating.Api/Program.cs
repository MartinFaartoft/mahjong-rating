using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Rating.Api.Infrastructure;
using Rating.Application;
using Rating.Infrastructure;
using Rating.Infrastructure.Auth;
using Rating.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        // Serialize/deserialize Ruleset (and other enums) as their string names.
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// OpenTelemetry: metrics + traces + logs → OTLP HTTP (endpoint from env).
// If OTEL_EXPORTER_OTLP_ENDPOINT is unset, exporters become no-ops, which is
// what we want for `dotnet run` locally without an Alloy collector.
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(
        serviceName: builder.Environment.ApplicationName,
        serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString()))
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter("Npgsql")
        .AddOtlpExporter())
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource("Npgsql")
        .AddOtlpExporter());

builder.Logging.AddOpenTelemetry(o =>
{
    o.IncludeFormattedMessage = true;
    o.IncludeScopes = true;
    o.AddOtlpExporter();
});

var app = builder.Build();

// Apply pending migrations + seed the admin user on startup.
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    var db = sp.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await AdminUserSeeder.SeedAsync(
        db,
        sp.GetRequiredService<IOptions<AdminOptions>>(),
        sp.GetRequiredService<AdminUserAccessor>(),
        sp.GetRequiredService<ILogger<Program>>());
}

app.UseExceptionHandler(new ExceptionHandlerOptions { ExceptionHandler = ProblemExceptionHandler.HandleAsync });

app.UseAuthentication();
app.UseAuthorization();

// Liveness — no DB dependency, used by container healthcheck.
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

// Readiness / DB smoke test — runs a trivial query via EF.
app.MapGet("/probe", async (AppDbContext db, ILogger<Program> logger) =>
{
    var result = await db.Database.SqlQueryRaw<int>("SELECT 1 + 1 AS \"Value\"").SingleAsync();
    logger.LogInformation("probe ok, result={Result}", result);
    return Results.Ok(new { db = "up", one_plus_one = result });
});

app.MapControllers();

app.Run();
