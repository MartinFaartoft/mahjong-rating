using Rating.Domain.Entities;
using Rating.Domain.Enums;

namespace Rating.Application.Abstractions;

public interface IRatingHistoryRepository
{
    void Add(RatingHistoryEntry entry);
    /// <summary>
    /// Removes rating history entries for the given games. Used before a replay
    /// to clear stale entries for games about to be recomputed.
    /// </summary>
    Task DeleteForGamesAsync(IReadOnlyCollection<Guid> gameIds, CancellationToken ct = default);
    /// <summary>
    /// Current rating for each player in <paramref name="playerIds"/> for the
    /// given ruleset. Players with no entries are omitted (callers should
    /// treat missing as 0).
    /// </summary>
    Task<IReadOnlyDictionary<Guid, decimal>> GetCurrentRatingsAsync(
        Ruleset ruleset, IReadOnlyCollection<Guid> playerIds, CancellationToken ct = default);
    Task<IReadOnlyList<(Guid PlayerId, decimal RatingAfter)>> GetAllCurrentAsync(
        Ruleset ruleset, CancellationToken ct = default);
    Task<IReadOnlyList<RatingHistoryEntry>> GetHistoryAsync(
        Ruleset ruleset, Guid playerId, CancellationToken ct = default);
}
