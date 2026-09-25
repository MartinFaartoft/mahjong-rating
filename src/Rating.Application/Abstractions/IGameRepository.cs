using Rating.Domain.Entities;
using Rating.Domain.Enums;

namespace Rating.Application.Abstractions;

public interface IGameRepository
{
    void Add(Game game);
    void Remove(Game game);
    Task<Game?> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Game>> ListAsync(Ruleset? ruleset, int limit, DateTimeOffset? before, CancellationToken ct = default);
    /// <summary>
    /// All games for a ruleset with <see cref="Game.FinishedAt"/> at or after
    /// <paramref name="from"/>, ordered ascending by (FinishedAt, CreatedAt, Id).
    /// </summary>
    Task<IReadOnlyList<Game>> GetForReplayAsync(Ruleset ruleset, DateTimeOffset from, CancellationToken ct = default);
}
