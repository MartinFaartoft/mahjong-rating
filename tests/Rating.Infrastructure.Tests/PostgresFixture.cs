using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Rating.Application;
using Rating.Application.Abstractions;
using Rating.Domain.Entities;
using Rating.Infrastructure;
using Rating.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Rating.Infrastructure.Tests;

/// <summary>
/// Spins up a real Postgres container once per test class and hands out a
/// disposable <see cref="IServiceScope"/> per test. Migrations are applied on
/// first use so tests hit exactly the same schema the API does.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("rating")
        .WithUsername("rating")
        .WithPassword("dev")
        .Build();

    public ServiceProvider Services { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _container.GetConnectionString(),
            })
            .Build();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddInfrastructure(config);
        services.AddApplication();

        // Replace the HttpContext-backed ICurrentUser with a per-scope mutable
        // stub so tests can control the caller identity directly.
        services.RemoveAll<ICurrentUser>();
        services.AddScoped<ICurrentUser, MutableCurrentUser>();

        Services = services.BuildServiceProvider();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await Services.DisposeAsync();
        await _container.DisposeAsync();
    }
}

/// <summary>
/// Seeds a single test user for FK on Game.CreatedByUserId. Returned via helpers.
/// </summary>
public static class TestSeed
{
    public static async Task<User> SeedUserAsync(AppDbContext db, ICurrentUser currentUser)
    {
        var user = new User { Id = Guid.NewGuid(), Username = $"tester-{Guid.NewGuid():N}", IsAdmin = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        ((MutableCurrentUser)currentUser).UserId = user.Id;
        ((MutableCurrentUser)currentUser).IsAdmin = true;
        ((MutableCurrentUser)currentUser).Username = user.Username;
        return user;
    }

    public static async Task<Player> SeedPlayerAsync(AppDbContext db, string name)
    {
        var p = new Player { Id = Guid.NewGuid(), DisplayName = name };
        db.Players.Add(p);
        await db.SaveChangesAsync();
        return p;
    }
}

/// <summary>
/// Per-scope stand-in for <see cref="ICurrentUser"/>. Tests mutate the
/// properties directly to simulate a specific caller.
/// </summary>
public sealed class MutableCurrentUser : ICurrentUser
{
    public Guid? UserId { get; set; }
    public string? Username { get; set; }
    public bool IsAdmin { get; set; }
}
