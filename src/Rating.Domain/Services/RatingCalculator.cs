using Rating.Domain.Enums;

namespace Rating.Domain.Services;

/// <summary>
/// Pure rating calculation. No I/O, no state. Inputs are player scores and their
/// current ratings; output is each player's new rating after the game.
/// </summary>
public static class RatingCalculator
{
    /// <summary>
    /// Compute new ratings for every participant of a single game.
    /// </summary>
    /// <param name="ruleset">Game ruleset.</param>
    /// <param name="numberOfWinds">Number of winds played (must be positive).</param>
    /// <param name="oldRatings">Each participant's rating before the game (0 for first-time players).</param>
    /// <param name="scores">Each participant's score for the game.</param>
    /// <returns>New rating per player. Keys match <paramref name="oldRatings"/>.</returns>
    public static IReadOnlyDictionary<Guid, decimal> ComputeNewRatings(
        Ruleset ruleset,
        int numberOfWinds,
        IReadOnlyDictionary<Guid, decimal> oldRatings,
        IReadOnlyDictionary<Guid, int> scores)
    {
        ArgumentNullException.ThrowIfNull(oldRatings);
        ArgumentNullException.ThrowIfNull(scores);
        if (numberOfWinds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(numberOfWinds), "Must be positive.");
        }
        if (oldRatings.Count == 0)
        {
            throw new ArgumentException("At least one participant required.", nameof(oldRatings));
        }
        if (oldRatings.Count != scores.Count || !oldRatings.Keys.All(scores.ContainsKey))
        {
            throw new ArgumentException("oldRatings and scores must cover the same players.");
        }

        var (glScore, glDamp, diffDenom) = GetFactors(ruleset, numberOfWinds, oldRatings.Count);
        // Damping matches the legacy mahjongdk implementation: 40 * gl + 1.
        // The +1 offset is a deliberate deviation from the plain-English spec
        // so the new system reproduces historical ratings bit-for-bit when
        // the full game history is replayed.
        var damp = 40m * glDamp + 1m;

        decimal diff = 0m;
        foreach (var r in oldRatings.Values) diff += r;
        diff /= diffDenom;

        var result = new Dictionary<Guid, decimal>(oldRatings.Count);
        foreach (var (playerId, old) in oldRatings)
        {
            var score = scores[playerId];
            var newRating = old + (score * glScore + diff - old) / damp;
            result[playerId] = newRating;
        }
        return result;
    }

    /// <summary>
    /// Returns the per-ruleset legacy factors, reverse-engineered from the
    /// full mahjongdk game archive (10k+ MCR games, 10k+ Riichi games):
    /// <list type="bullet">
    ///   <item>MCR: score and damping both use gl = 4/winds. The difficulty
    ///     average divides by max(4, playerCount) -- legacy assumed a full
    ///     4-seat table when computing the average, matters only for a
    ///     single 3-player game in the archive.</item>
    ///   <item>Riichi: score uses gl = 2/winds. Damping uses the same gl
    ///     for 4+ player tables but a doubled gl (= 4/winds) for 3-player
    ///     tables -- the "3-player gl is doubled before the *40 + 1" rule
    ///     mentioned in the legacy formula description. Difficulty average
    ///     uses the actual player count.</item>
    /// </list>
    /// </summary>
    private static (decimal glScore, decimal glDamp, int diffDenom) GetFactors(
        Ruleset ruleset, int numberOfWinds, int playerCount) => ruleset switch
    {
        Ruleset.Mcr => (4m / numberOfWinds, 4m / numberOfWinds, Math.Max(4, playerCount)),
        Ruleset.Riichi => (
            2m / numberOfWinds,
            (playerCount == 3 ? 4m : 2m) / numberOfWinds,
            playerCount),
        _ => throw new ArgumentOutOfRangeException(nameof(ruleset), ruleset, "Unknown ruleset."),
    };
}
