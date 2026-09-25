using Microsoft.EntityFrameworkCore;
using Rating.Application.Abstractions;
using Rating.Domain.Entities;
using Rating.Domain.Enums;

namespace Rating.Infrastructure.Persistence.Repositories;

internal sealed class RatingHistoryRepository(AppDbContext db) : IRatingHistoryRepository
{
    public void Add(RatingHistoryEntry entry) => db.RatingHistory.Add(entry);

    public async Task DeleteForGamesAsync(IReadOnlyCollection<Guid> gameIds, CancellationToken ct = default)
    {
        if (gameIds.Count == 0) return;
        await db.RatingHistory.Where(r => gameIds.Contains(r.GameId)).ExecuteDeleteAsync(ct);
    }

    public async Task<IReadOnlyDictionary<Guid, decimal>> GetCurrentRatingsAsync(
        Ruleset ruleset, IReadOnlyCollection<Guid> playerIds, CancellationToken ct = default)
    {
        if (playerIds.Count == 0) return new Dictionary<Guid, decimal>();

        // Order by (FinishedAt, CreatedAt, Id) of the source game so ties are
        // broken deterministically, matching the replay order.
        var rows = await (from rh in db.RatingHistory
                          join g in db.Games on rh.GameId equals g.Id
                          where rh.Ruleset == ruleset && playerIds.Contains(rh.PlayerId)
                          select new
                          {
                              rh.PlayerId,
                              rh.RatingAfter,
                              g.FinishedAt,
                              g.CreatedAt,
                              GameId = g.Id,
                          }).ToListAsync(ct);

        return rows.GroupBy(r => r.PlayerId)
            .ToDictionary(
                grp => grp.Key,
                grp => grp.OrderByDescending(r => r.FinishedAt)
                          .ThenByDescending(r => r.CreatedAt)
                          .ThenByDescending(r => r.GameId)
                          .First().RatingAfter);
    }

    public async Task<IReadOnlyList<(Guid PlayerId, decimal RatingAfter)>> GetAllCurrentAsync(
        Ruleset ruleset, CancellationToken ct = default)
    {
        var rows = await (from rh in db.RatingHistory
                          join g in db.Games on rh.GameId equals g.Id
                          where rh.Ruleset == ruleset
                          select new
                          {
                              rh.PlayerId,
                              rh.RatingAfter,
                              g.FinishedAt,
                              g.CreatedAt,
                              GameId = g.Id,
                          }).ToListAsync(ct);

        return rows.GroupBy(r => r.PlayerId)
            .Select(grp => (grp.Key, grp.OrderByDescending(r => r.FinishedAt)
                                        .ThenByDescending(r => r.CreatedAt)
                                        .ThenByDescending(r => r.GameId)
                                        .First().RatingAfter))
            .ToList();
    }

    public async Task<IReadOnlyList<RatingHistoryEntry>> GetHistoryAsync(
        Ruleset ruleset, Guid playerId, CancellationToken ct = default)
        => await db.RatingHistory
            .Where(r => r.Ruleset == ruleset && r.PlayerId == playerId)
            .OrderBy(r => r.ComputedAt)
            .ToListAsync(ct);
}
