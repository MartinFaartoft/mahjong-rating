using Npgsql;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// OpenTelemetry: metrics + traces + logs → OTLP HTTP (endpoint from env).
// If OTEL_EXPORTER_OTLP_ENDPOINT is unset, exporters become no-ops, which is
// what we want for `dotnet run` locally without an Alloy collector.
var otel = builder.Services.AddOpenTelemetry()
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
        .AddNpgsql()
        .AddOtlpExporter());

builder.Logging.AddOpenTelemetry(o =>
{
    o.IncludeFormattedMessage = true;
    o.IncludeScopes = true;
    o.AddOtlpExporter();
});

var app = builder.Build();

// Liveness — no DB dependency, used by container healthcheck.
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

// Readiness / DB smoke test — runs a trivial query.
app.MapGet("/probe", async (ILogger<Program> logger) =>
{
    var cs = app.Configuration.GetConnectionString("Default");
    if (string.IsNullOrWhiteSpace(cs))
    {
        return Results.Problem("ConnectionStrings:Default is not configured");
    }

    await using var conn = new NpgsqlConnection(cs);
    await conn.OpenAsync();
    await using var cmd = new NpgsqlCommand("SELECT 1 + 1", conn);
    var result = (int)(await cmd.ExecuteScalarAsync() ?? 0);
    logger.LogInformation("probe ok, result={Result}", result);
    return Results.Ok(new { db = "up", one_plus_one = result });
});

app.Run();
