using Npgsql;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Liveness — no DB dependency, used by container healthcheck.
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

// Readiness / DB smoke test — runs a trivial query.
app.MapGet("/probe", async () =>
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
    return Results.Ok(new { db = "up", one_plus_one = result });
});

app.Run();
