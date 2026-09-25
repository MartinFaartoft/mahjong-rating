using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace Rating.Api.Tests;

/// <summary>
/// Boots the Api against a fresh Postgres container per test collection.
/// The container is started once for all tests that share this fixture;
/// each test that mutates state runs in its own scope and cleans up
/// records it created (or is order-independent by construction, e.g.
/// distinct player names).
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public const string AdminUsername = "admin";
    public const string AdminPassword = "test";

    public async Task InitializeAsync()
    {
        await _pg.StartAsync();

        // Configure via environment variables so they are visible to
        // `WebApplication.CreateBuilder(args)` before ConfigureWebHost hooks
        // run. Minimal hosting evaluates config eagerly at builder creation
        // time, so any override added via ConfigureAppConfiguration alone
        // would be applied too late for AddInfrastructure to see it.
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", _pg.GetConnectionString());
        Environment.SetEnvironmentVariable("Admin__Username", AdminUsername);
        Environment.SetEnvironmentVariable("Admin__Password", AdminPassword);

        // Force lazy host creation now so migration + admin seed run as part
        // of fixture init (failures surface here, not inside a test).
        _ = CreateClient();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", null);
        Environment.SetEnvironmentVariable("Admin__Username", null);
        Environment.SetEnvironmentVariable("Admin__Password", null);
        await _pg.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Testing env skips launchSettings and appsettings.Development.json.
        builder.UseEnvironment("Testing");
    }

    /// <summary>
    /// Creates an HttpClient with a Basic auth header for the seeded admin user.
    /// </summary>
    public HttpClient CreateAdminClient()
    {
        var client = CreateClient();
        var creds = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{AdminUsername}:{AdminPassword}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", creds);
        return client;
    }
}

[CollectionDefinition(nameof(ApiCollection))]
public sealed class ApiCollection : ICollectionFixture<ApiFactory> { }
