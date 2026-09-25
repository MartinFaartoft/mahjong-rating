using Rating.Application.Dto;
using Rating.Domain.Enums;

namespace Rating.Application.Services;

public interface IRatingService
{
    Task<IReadOnlyList<RatingDto>> GetCurrentAsync(Ruleset ruleset, CancellationToken ct = default);
    Task<IReadOnlyList<RatingHistoryDto>> GetHistoryAsync(Ruleset ruleset, Guid playerId, CancellationToken ct = default);

    /// <summary>
    /// Replays all games of <paramref name="ruleset"/> with FinishedAt at or
    /// after <paramref name="from"/>, rebuilding their rating history entries.
    /// Called by <see cref="IGameService"/> after any mutation. Not exposed via HTTP.
    /// </summary>
    Task RecomputeFromAsync(Ruleset ruleset, DateTimeOffset from, CancellationToken ct = default);
}
