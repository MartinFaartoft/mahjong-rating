using Rating.Application.Abstractions;
using Rating.Application.Dto;
using Rating.Domain.Entities;
using Rating.Domain.Enums;
using Rating.Domain.Services;

namespace Rating.Application.Services;

public sealed class RatingService(
    IGameRepository games,
    IPlayerRepository players,
    IRatingHistoryRepository history) : IRatingService
{
    public async Task<IReadOnlyList<RatingDto>> GetCurrentAsync(Ruleset ruleset, CancellationToken ct = default)
    {
        var current = await history.GetAllCurrentAsync(ruleset, ct);
        if (current.Count == 0) return Array.Empty<RatingDto>();

        var byId = (await players.ListAsync(ct)).ToDictionary(p => p.Id);
        return current
            .Where(c => byId.ContainsKey(c.PlayerId))
            .Select(c => new RatingDto(c.PlayerId, byId[c.PlayerId].DisplayName, ruleset, c.RatingAfter))
            .OrderByDescending(r => r.Rating)
            .ToList();
    }

    public async Task<IReadOnlyList<RatingHistoryDto>> GetHistoryAsync(Ruleset ruleset, Guid playerId, CancellationToken ct = default)
    {
        var rows = await history.GetHistoryAsync(ruleset, playerId, ct);
        return rows.Select(r => new RatingHistoryDto(r.GameId, r.Ruleset, r.RatingAfter, r.ComputedAt)).ToList();
    }

    public async Task RecomputeFromAsync(Ruleset ruleset, DateTimeOffset from, CancellationToken ct = default)
    {
        // 1. Load games in replay order.
        var toReplay = await games.GetForReplayAsync(ruleset, from, ct);
        if (toReplay.Count == 0) return;

        // 2. Wipe existing history for those games. Prior-game ratings for
        //    participants are preserved and become the "old" ratings on replay.
        await history.DeleteForGamesAsync(toReplay.Select(g => g.Id).ToList(), ct);

        // 3. Seed running ratings once from DB (games strictly before `from`).
        //    We can't re-query mid-loop because history entries added in this
        //    method aren't persisted until the caller's SaveChangesAsync.
        var allParticipants = toReplay.SelectMany(g => g.Results).Select(r => r.PlayerId).Distinct().ToList();
        var seed = await history.GetCurrentRatingsAsync(ruleset, allParticipants, ct);
        var running = new Dictionary<Guid, decimal>(seed);

        // 4. Replay chronologically, threading the running dictionary through.
        foreach (var game in toReplay)
        {
            var participants = game.Results.Select(r => r.PlayerId).ToList();
            var oldRatings = participants.ToDictionary(
                id => id,
                id => running.TryGetValue(id, out var r) ? r : 0m);
            var scores = game.Results.ToDictionary(r => r.PlayerId, r => r.Score);

            var newRatings = RatingCalculator.ComputeNewRatings(ruleset, game.NumberOfWinds, oldRatings, scores);

            foreach (var (playerId, rating) in newRatings)
            {
                history.Add(new RatingHistoryEntry
                {
                    Id = Guid.NewGuid(),
                    PlayerId = playerId,
                    Ruleset = ruleset,
                    GameId = game.Id,
                    RatingAfter = rating,
                    // Use FinishedAt so "latest per player" queries order correctly.
                    ComputedAt = game.FinishedAt,
                });
                running[playerId] = rating;
            }
        }
        // Caller (GameService) is responsible for calling IUnitOfWork.SaveChangesAsync.
    }
}
