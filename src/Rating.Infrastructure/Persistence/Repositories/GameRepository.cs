using Microsoft.EntityFrameworkCore;
using Rating.Application.Abstractions;
using Rating.Domain.Entities;
using Rating.Domain.Enums;

namespace Rating.Infrastructure.Persistence.Repositories;

internal sealed class GameRepository(AppDbContext db) : IGameRepository
{
    public void Add(Game game) => db.Games.Add(game);
    public void Remove(Game game) => db.Games.Remove(game);

    public Task<Game?> GetAsync(Guid id, CancellationToken ct = default)
        => db.Games.Include(g => g.Results).FirstOrDefaultAsync(g => g.Id == id, ct);

    public async Task<IReadOnlyList<Game>> ListAsync(Ruleset? ruleset, int limit, DateTimeOffset? before, CancellationToken ct = default)
    {
        var q = db.Games.Include(g => g.Results).AsQueryable();
        if (ruleset.HasValue) q = q.Where(g => g.Ruleset == ruleset.Value);
        if (before.HasValue) q = q.Where(g => g.FinishedAt < before.Value);
        return await q
            .OrderByDescending(g => g.FinishedAt)
            .ThenByDescending(g => g.CreatedAt)
            .ThenByDescending(g => g.Id)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Game>> GetForReplayAsync(Ruleset ruleset, DateTimeOffset from, CancellationToken ct = default)
        => await db.Games
            .Include(g => g.Results)
            .Where(g => g.Ruleset == ruleset && g.FinishedAt >= from)
            .OrderBy(g => g.FinishedAt)
            .ThenBy(g => g.CreatedAt)
            .ThenBy(g => g.Id)
            .ToListAsync(ct);
}
