using Rating.Domain.Entities;

namespace Rating.Application.Abstractions;

public interface IPlayerRepository
{
    void Add(Player player);
    Task<Player?> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Player>> ListAsync(CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> ExistingIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
}
