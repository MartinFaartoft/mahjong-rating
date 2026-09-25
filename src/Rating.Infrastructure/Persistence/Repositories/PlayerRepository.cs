using Microsoft.EntityFrameworkCore;
using Rating.Application.Abstractions;
using Rating.Domain.Entities;

namespace Rating.Infrastructure.Persistence.Repositories;

internal sealed class PlayerRepository(AppDbContext db) : IPlayerRepository
{
    public void Add(Player player) => db.Players.Add(player);

    public Task<Player?> GetAsync(Guid id, CancellationToken ct = default)
        => db.Players.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<Player>> ListAsync(CancellationToken ct = default)
        => await db.Players.OrderBy(p => p.DisplayName).ToListAsync(ct);

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
        => db.Players.AnyAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<Guid>> ExistingIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
        => await db.Players.Where(p => ids.Contains(p.Id)).Select(p => p.Id).ToListAsync(ct);
}
