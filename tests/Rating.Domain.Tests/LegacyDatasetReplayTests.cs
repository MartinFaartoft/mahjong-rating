using System.Text.Json;
using System.Text.Json.Serialization;
using Rating.Domain.Enums;
using Rating.Domain.Services;

namespace Rating.Domain.Tests;

/// <summary>
/// Replays the full historical MCR and Riichi game archives scraped from
/// mahjongdk.dk (10k+ games per ruleset, 2001-2026) through
/// <see cref="RatingCalculator"/> and asserts every per-player rating
/// matches the legacy system's output.
///
/// This is our ground-truth check: if the calculator formula, ordering, or
/// precision handling ever regresses, this test surfaces it immediately.
/// Datasets committed under <c>data/</c>; source:
/// https://github.com/MartinFaartoft/MahjongDkScraper/tree/main/data
/// </summary>
public sealed class LegacyDatasetReplayTests
{
    // Legacy stores ratings as double (~15 significant digits). We compute
    // in decimal (~28 digits). Empirically all games agree to well under
    // 1e-9; use 1e-9 as the tolerance for both per-game and running-replay
    // comparisons. Riichi ratings are much larger in magnitude (thousands
    // vs tens) so we scale by max(1, |legacy|) to keep the comparison a
    // relative tolerance.
    private const decimal PerGameTolerance = 0.000000001m; // 1e-9

    public static IEnumerable<object[]> Datasets => new[]
    {
        new object[] { Ruleset.Mcr, "mcr_games_full.json" },
        new object[] { Ruleset.Riichi, "riichi_games_full.json" },
    };

    /// <summary>
    /// For each game, feed the dataset's own <c>OldRating</c> for each
    /// player and assert the calculator reproduces the dataset's
    /// <c>NewRating</c>. This isolates the per-game formula (independent
    /// of error accumulation).
    /// </summary>
    [Theory]
    [MemberData(nameof(Datasets))]
    public void PerGameFormula_MatchesLegacyForEveryGame(Ruleset ruleset, string dataFile)
    {
        var games = LoadDataset(dataFile);
        var deviations = new List<(string GameId, string Player, decimal Diff)>();

        foreach (var g in games)
        {
            var ids = g.Players.Select(_ => Guid.NewGuid()).ToArray();
            var oldRatings = new Dictionary<Guid, decimal>(g.Players.Count);
            var scores = new Dictionary<Guid, int>(g.Players.Count);
            for (var i = 0; i < g.Players.Count; i++)
            {
                oldRatings[ids[i]] = (decimal)g.Players[i].OldRating;
                scores[ids[i]] = g.Players[i].Score;
            }

            var next = RatingCalculator.ComputeNewRatings(ruleset, g.NumberOfWinds, oldRatings, scores);

            for (var i = 0; i < g.Players.Count; i++)
            {
                var expected = (decimal)g.Players[i].NewRating;
                var scale = Math.Max(1m, Math.Abs(expected));
                var diff = Math.Abs(next[ids[i]] - expected) / scale;
                if (diff > PerGameTolerance)
                {
                    deviations.Add((g.Id, g.Players[i].Name, diff));
                }
            }
        }

        Assert.Empty(deviations);
    }

    /// <summary>
    /// Full running replay: maintain a per-player rating dictionary, thread
    /// each game's output back in as the next game's input, and assert the
    /// final per-player rating matches the last <c>NewRating</c> observed
    /// for that player in the legacy dataset.
    ///
    /// Excluded players: some legacy datasets contain occasional carry-forward
    /// inconsistencies where a player's <c>OldRating</c> in a game does not
    /// equal their <c>NewRating</c> from the immediately previous game. This
    /// is legacy data noise, not a formula deviation. Any player affected by
    /// such a discontinuity, and anyone who later sits at their table,
    /// inherits the taint and is excluded from the final comparison.
    /// </summary>
    [Theory]
    [MemberData(nameof(Datasets))]
    public void RunningReplay_ProducesLegacyRatings_ForEveryUntaintedPlayer(Ruleset ruleset, string dataFile)
    {
        var games = LoadDataset(dataFile);

        var ratings = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var lastLegacyRating = new Dictionary<string, double>(StringComparer.Ordinal);
        var tainted = new HashSet<string>(StringComparer.Ordinal);

        foreach (var g in games)
        {
            // Same player at two seats occurs sporadically in legacy data.
            // Give each seat its own Guid so the calculator sees distinct keys.
            var ids = new Guid[g.Players.Count];
            var oldRatings = new Dictionary<Guid, decimal>(g.Players.Count);
            var scores = new Dictionary<Guid, int>(g.Players.Count);
            for (var i = 0; i < g.Players.Count; i++)
            {
                ids[i] = Guid.NewGuid();
                var carried = ratings.GetValueOrDefault(g.Players[i].Name, 0m);
                oldRatings[ids[i]] = carried;
                scores[ids[i]] = g.Players[i].Score;

                // If the legacy dataset's OldRating disagrees with the value
                // we carried forward, legacy has an out-of-band rating change
                // we cannot reproduce; taint the player.
                var legacyOld = (decimal)g.Players[i].OldRating;
                var scale = Math.Max(1m, Math.Abs(legacyOld));
                if (Math.Abs(carried - legacyOld) / scale > PerGameTolerance)
                {
                    tainted.Add(g.Players[i].Name);
                }
            }

            var next = RatingCalculator.ComputeNewRatings(ruleset, g.NumberOfWinds, oldRatings, scores);

            var tableTainted = g.Players.Any(p => tainted.Contains(p.Name));
            for (var i = 0; i < g.Players.Count; i++)
            {
                var p = g.Players[i];
                ratings[p.Name] = next[ids[i]];
                lastLegacyRating[p.Name] = p.NewRating;
                if (tableTainted)
                {
                    tainted.Add(p.Name);
                }
            }
        }

        var mismatches = ratings
            .Where(kv => !tainted.Contains(kv.Key))
            .Select(kv => (Name: kv.Key, Ours: kv.Value, Legacy: (decimal)lastLegacyRating[kv.Key]))
            .Where(t => Math.Abs(t.Ours - t.Legacy) / Math.Max(1m, Math.Abs(t.Legacy)) > PerGameTolerance)
            .OrderByDescending(t => Math.Abs(t.Ours - t.Legacy))
            .ToList();

        Assert.True(
            mismatches.Count == 0,
            $"[{ruleset}] Untainted players diverging from legacy final rating " +
            $"(untainted={ratings.Count - tainted.Count}, tainted={tainted.Count}): " +
            string.Join(", ", mismatches.Take(5).Select(m => $"{m.Name} ours={m.Ours} legacy={m.Legacy}")));
    }

    private static List<LegacyGame> LoadDataset(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "data", fileName);
        Assert.True(File.Exists(path), $"Dataset not found at {path}");
        using var stream = File.OpenRead(path);
        var games = JsonSerializer.Deserialize<List<LegacyGame>>(stream, JsonOpts)!;
        Assert.NotEmpty(games);
        return games;
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private sealed record LegacyGame(
        string Id,
        string DateOfGame,
        int NumberOfWinds,
        double Difficulty,
        List<LegacyPlayer> Players);

    private sealed record LegacyPlayer(
        string Name,
        int Score,
        double OldRating,
        double NewRating);
}
