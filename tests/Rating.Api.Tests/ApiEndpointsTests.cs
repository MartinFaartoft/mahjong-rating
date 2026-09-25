using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rating.Api.Tests;

[Collection(nameof(ApiCollection))]
public sealed class ApiEndpointsTests(ApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task Healthz_ReturnsOk()
    {
        var client = factory.CreateClient();
        var resp = await client.GetAsync("/healthz");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Players_AnonymousList_ReturnsOk()
    {
        var client = factory.CreateClient();
        var resp = await client.GetAsync("/players");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Players_UnauthenticatedCreate_Returns401()
    {
        var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/players",
            new { displayName = $"anon-{Guid.NewGuid():N}" });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Players_AdminCreate_ThenGet_RoundTrips()
    {
        var admin = factory.CreateAdminClient();
        var name = $"alice-{Guid.NewGuid():N}";

        var create = await admin.PostAsJsonAsync("/players", new { displayName = name });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<PlayerResponse>(Json);
        Assert.NotNull(created);
        Assert.Equal(name, created!.DisplayName);

        var client = factory.CreateClient();
        var get = await client.GetFromJsonAsync<PlayerResponse>($"/players/{created.Id}", Json);
        Assert.Equal(created.Id, get!.Id);
        Assert.Equal(name, get.DisplayName);
    }

    [Fact]
    public async Task Games_FullFlow_ComputesExpectedRatings()
    {
        var admin = factory.CreateAdminClient();
        var client = factory.CreateClient();

        // Seed 4 players.
        var players = new List<PlayerResponse>();
        for (var i = 0; i < 4; i++)
        {
            var resp = await admin.PostAsJsonAsync("/players",
                new { displayName = $"flow-p{i}-{Guid.NewGuid():N}" });
            Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
            players.Add((await resp.Content.ReadFromJsonAsync<PlayerResponse>(Json))!);
        }

        // Add a 4-wind Mcr game: scores 40/-40/0/0 among all-new players.
        // Legacy formula: gl=1, damp=41, diff=0. Expected deltas 40/41, -40/41, 0, 0.
        var gameResp = await admin.PostAsJsonAsync("/games", new
        {
            ruleset = "Mcr",
            numberOfWinds = 4,
            finishedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            results = new[]
            {
                new { playerId = players[0].Id, score = 40, seatOrder = 0 },
                new { playerId = players[1].Id, score = -40, seatOrder = 1 },
                new { playerId = players[2].Id, score = 0, seatOrder = 2 },
                new { playerId = players[3].Id, score = 0, seatOrder = 3 },
            },
        });
        Assert.Equal(HttpStatusCode.Created, gameResp.StatusCode);
        var game = await gameResp.Content.ReadFromJsonAsync<GameResponse>(Json);
        Assert.NotNull(game);
        Assert.Equal("Mcr", game!.Ruleset);

        // Check ratings via the public endpoint.
        var ratings = (await client.GetFromJsonAsync<List<RatingResponse>>("/ratings/Mcr", Json))!
            .Where(r => players.Any(p => p.Id == r.PlayerId))
            .ToDictionary(r => r.PlayerId);

        const decimal expectedPos = 40m / 41m;
        Assert.Equal(expectedPos, ratings[players[0].Id].Rating, 10);
        Assert.Equal(-expectedPos, ratings[players[1].Id].Rating, 10);
        Assert.Equal(0m, ratings[players[2].Id].Rating, 10);
        Assert.Equal(0m, ratings[players[3].Id].Rating, 10);

        // History for player 0 should contain exactly the game we just added.
        var history = (await client.GetFromJsonAsync<List<HistoryResponse>>(
            $"/ratings/Mcr/players/{players[0].Id}/history", Json))!;
        Assert.Single(history);
        Assert.Equal(game.Id, history[0].GameId);
        Assert.Equal(expectedPos, history[0].RatingAfter, 10);

        // Delete the game -> ratings for those players return to zero.
        var del = await admin.DeleteAsync($"/games/{game.Id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        var after = (await client.GetFromJsonAsync<List<RatingResponse>>("/ratings/Mcr", Json))!;
        Assert.DoesNotContain(after, r => players.Any(p => p.Id == r.PlayerId));
    }

    [Fact]
    public async Task Games_UnauthenticatedCreate_Returns401()
    {
        var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/games", new
        {
            ruleset = "Mcr",
            numberOfWinds = 4,
            finishedAt = DateTimeOffset.UtcNow,
            results = Array.Empty<object>(),
        });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Games_GetNonexistent_Returns404()
    {
        var client = factory.CreateClient();
        var resp = await client.GetAsync($"/games/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    private sealed record PlayerResponse(Guid Id, string DisplayName);
    private sealed record GameResponse(Guid Id, string Ruleset, int NumberOfWinds, DateTimeOffset FinishedAt);
    private sealed record RatingResponse(Guid PlayerId, string DisplayName, string Ruleset, decimal Rating);
    private sealed record HistoryResponse(Guid GameId, string Ruleset, decimal RatingAfter, DateTimeOffset ComputedAt);
}
