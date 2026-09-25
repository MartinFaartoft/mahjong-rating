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

        var gl = GameLengthFactor(ruleset, numberOfWinds);
        // Damping matches the legacy mahjongdk implementation: 40 * game_length_factor + 1.
        // The +1 offset is a deliberate deviation from the plain-English spec so that the
        // new system reproduces historical ratings bit-for-bit when the full game history
        // is replayed.
        var damp = 40m * gl + 1m;

        // game_difficulty = average of participants' old ratings, but the legacy
        // impl assumed a full 4-seat table when computing the denominator: it
        // divides by max(4, playerCount) rather than playerCount. For the usual
        // 4+ seat games this is identical to a plain average; for sub-4-seat
        // tables (only one such game exists in 22 years of history) the legacy
        // formula pulls diff toward zero. We preserve the quirk so full replay
        // matches legacy on every game.
        var denom = Math.Max(4, oldRatings.Count);
        decimal diff = 0m;
        foreach (var r in oldRatings.Values) diff += r;
        diff /= denom;

        var result = new Dictionary<Guid, decimal>(oldRatings.Count);
        foreach (var (playerId, old) in oldRatings)
        {
            var score = scores[playerId];
            var newRating = old + (score * gl + diff - old) / damp;
            result[playerId] = newRating;
        }
        return result;
    }

    private static decimal GameLengthFactor(Ruleset ruleset, int numberOfWinds) => ruleset switch
    {
        Ruleset.Mcr => 4m / numberOfWinds,
        Ruleset.Riichi => 2m / numberOfWinds,
        _ => throw new ArgumentOutOfRangeException(nameof(ruleset), ruleset, "Unknown ruleset."),
    };
}
