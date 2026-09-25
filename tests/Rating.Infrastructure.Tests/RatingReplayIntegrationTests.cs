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

        Assert.Equal(1m, current[p1.Id].Rating);
        Assert.Equal(-1m, current[p2.Id].Rating);
        Assert.Equal(0m, current[p3.Id].Rating);
        Assert.Equal(0m, current[p4.Id].Rating);
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

        // After G_later: a=1, b=-1 (Riichi 1-wind: gl=2, damp=80, diff=0).
        var afterFirst = (await ratings.GetCurrentAsync(Ruleset.Riichi)).ToDictionary(r => r.PlayerId);
        Assert.Equal(1m, afterFirst[a.Id].Rating);
        Assert.Equal(-1m, afterFirst[b.Id].Rating);

        // Insert G_earlier at t=-1h with a and b starting from 0. This must
        // trigger a replay of G_later using the ratings produced by G_earlier.
        await games.AddAsync(new AddGameRequest(
            Ruleset.Riichi, 1, t0.AddHours(-1),
            new[] { new GameResultRequest(a.Id, -40, 0), new GameResultRequest(b.Id, 40, 1) }));

        // Manually compute the expected chain:
        // G_earlier: gl=2, damp=80, diff=0.
        //   a_new = 0 + (-40*2 + 0 - 0)/80 = -1
        //   b_new = 0 + ( 40*2 + 0 - 0)/80 =  1
        // G_later: diff = avg(-1, 1) = 0.
        //   a_new = -1 + (40*2 + 0 - (-1))/80 = -1 + 81/80 = -1 + 1.0125 = 0.0125
        //   b_new =  1 + (-40*2 + 0 - 1)/80 =  1 + (-81/80) = 1 - 1.0125 = -0.0125
        var final = (await ratings.GetCurrentAsync(Ruleset.Riichi)).ToDictionary(r => r.PlayerId);
        Assert.Equal(0.0125m, final[a.Id].Rating);
        Assert.Equal(-0.0125m, final[b.Id].Rating);
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
        Assert.Equal(1m, current[a.Id].Rating);
        Assert.Equal(-1m, current[b.Id].Rating);
    }
}
