using Microsoft.Extensions.DependencyInjection;
using Rating.Application.Dto;
using Rating.Application.Services;
using Rating.Domain.Enums;
using Rating.Infrastructure.Persistence;

namespace Rating.Infrastructure.Tests;

public sealed class RatingReplayIntegrationTests(PostgresFixture fx) : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fx = fx;

    [Fact]
    public async Task AddingGame_ProducesExpectedRating_MatchesSpecExample()
    {
        await using var scope = _fx.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var games = scope.ServiceProvider.GetRequiredService<IGameService>();
        var ratings = scope.ServiceProvider.GetRequiredService<IRatingService>();

        var user = await TestSeed.SeedUserAsync(db, scope.ServiceProvider.GetRequiredService<Rating.Application.Abstractions.ICurrentUser>());
        var p1 = await TestSeed.SeedPlayerAsync(db, $"p1-{Guid.NewGuid():N}");
        var p2 = await TestSeed.SeedPlayerAsync(db, $"p2-{Guid.NewGuid():N}");
        var p3 = await TestSeed.SeedPlayerAsync(db, $"p3-{Guid.NewGuid():N}");
        var p4 = await TestSeed.SeedPlayerAsync(db, $"p4-{Guid.NewGuid():N}");

        await games.AddAsync(new AddGameRequest(
            Ruleset.Mcr, 4, DateTimeOffset.UtcNow.AddMinutes(-10),
            new[]
            {
                new GameResultRequest(p1.Id, 40, 0),
                new GameResultRequest(p2.Id, -40, 1),
                new GameResultRequest(p3.Id, 0, 2),
                new GameResultRequest(p4.Id, 0, 3),
            }));

        var current = (await ratings.GetCurrentAsync(Ruleset.Mcr)).ToDictionary(r => r.PlayerId);

        // Mcr 4-wind: gl=1, damp = 40*gl + 1 = 41. diff=0. Ratings persist as
        // numeric(18,10), so compare to 10 decimal places.
        Assert.Equal(40m / 41m, current[p1.Id].Rating, 10);
        Assert.Equal(-40m / 41m, current[p2.Id].Rating, 10);
        Assert.Equal(0m, current[p3.Id].Rating, 10);
        Assert.Equal(0m, current[p4.Id].Rating, 10);
    }

    [Fact]
    public async Task InsertingPastGame_RecomputesForwardCorrectly()
    {
        await using var scope = _fx.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var games = scope.ServiceProvider.GetRequiredService<IGameService>();
        var ratings = scope.ServiceProvider.GetRequiredService<IRatingService>();

        var user = await TestSeed.SeedUserAsync(db, scope.ServiceProvider.GetRequiredService<Rating.Application.Abstractions.ICurrentUser>());
        var a = await TestSeed.SeedPlayerAsync(db, $"a-{Guid.NewGuid():N}");
        var b = await TestSeed.SeedPlayerAsync(db, $"b-{Guid.NewGuid():N}");

        // Add game G_later at t=+0. Both start at 0.
        var t0 = DateTimeOffset.UtcNow;
        await games.AddAsync(new AddGameRequest(
            Ruleset.Riichi, 1, t0,
            new[] { new GameResultRequest(a.Id, 40, 0), new GameResultRequest(b.Id, -40, 1) }));

        // After G_later: Riichi 1-wind => gl=2, damp = 40*gl + 1 = 81, diff=0.
        //   a = (40*2)/81 = 80/81, b = -80/81.
        var afterFirst = (await ratings.GetCurrentAsync(Ruleset.Riichi)).ToDictionary(r => r.PlayerId);
        Assert.Equal(80m / 81m, afterFirst[a.Id].Rating, 10);
        Assert.Equal(-80m / 81m, afterFirst[b.Id].Rating, 10);

        // Insert G_earlier at t=-1h with a and b starting from 0. This must
        // trigger a replay of G_later using the ratings produced by G_earlier.
        await games.AddAsync(new AddGameRequest(
            Ruleset.Riichi, 1, t0.AddHours(-1),
            new[] { new GameResultRequest(a.Id, -40, 0), new GameResultRequest(b.Id, 40, 1) }));

        // Manually compute the expected chain (gl=2, damp=81):
        //   G_earlier (both old=0, diff=0): a = -80/81, b = 80/81.
        //   G_later replayed with diff=0:
        //     a_new = -80/81 + (80 + 80/81)/81
        //           = (-80*81 + 80*82)/6561 = (-6480 + 6560)/6561 = 80/6561.
        //     b_new = -80/6561 (by symmetry).
        var final = (await ratings.GetCurrentAsync(Ruleset.Riichi)).ToDictionary(r => r.PlayerId);
        Assert.Equal(80m / 6561m, final[a.Id].Rating, 10);
        Assert.Equal(-80m / 6561m, final[b.Id].Rating, 10);
    }

    [Fact]
    public async Task DeletingGame_RecomputesForwardCorrectly()
    {
        await using var scope = _fx.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var games = scope.ServiceProvider.GetRequiredService<IGameService>();
        var ratings = scope.ServiceProvider.GetRequiredService<IRatingService>();

        var user = await TestSeed.SeedUserAsync(db, scope.ServiceProvider.GetRequiredService<Rating.Application.Abstractions.ICurrentUser>());
        var a = await TestSeed.SeedPlayerAsync(db, $"a-{Guid.NewGuid():N}");
        var b = await TestSeed.SeedPlayerAsync(db, $"b-{Guid.NewGuid():N}");

        var t0 = DateTimeOffset.UtcNow;
        var g1 = await games.AddAsync(new AddGameRequest(
            Ruleset.Mcr, 4, t0.AddHours(-2),
            new[] { new GameResultRequest(a.Id, 40, 0), new GameResultRequest(b.Id, -40, 1) }));
        await games.AddAsync(new AddGameRequest(
            Ruleset.Mcr, 4, t0,
            new[] { new GameResultRequest(a.Id, 40, 0), new GameResultRequest(b.Id, -40, 1) }));

        // Delete first game. Only the second remains: a=1, b=-1.
        await games.DeleteAsync(g1.Id);

        var current = (await ratings.GetCurrentAsync(Ruleset.Mcr)).ToDictionary(r => r.PlayerId);
        // Only one game remains: Mcr 4-wind, gl=1, damp=41, diff=0.
        Assert.Equal(40m / 41m, current[a.Id].Rating, 10);
        Assert.Equal(-40m / 41m, current[b.Id].Rating, 10);
    }
}
