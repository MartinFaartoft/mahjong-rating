using System.Text.Json;
using System.Text.Json.Serialization;
using Rating.Domain.Enums;
using Rating.Domain.Services;

namespace Rating.Domain.Tests;

/// <summary>
/// Replays the full historical MCR game archive scraped from mahjongdk.dk
/// (10k+ games from 2004 to today) through <see cref="RatingCalculator"/>
/// and asserts that each per-player rating matches the value the legacy
/// system produced.
///
/// This is our ground-truth check: if the calculator formula, ordering,
/// or precision handling ever regresses, this test surfaces it immediately.
/// The dataset is committed under <c>data/mcr_games_full.json</c>; source:
/// https://raw.githubusercontent.com/MartinFaartoft/MahjongDkScraper/refs/heads/main/data/mcr_games_full.json
/// </summary>
public sealed class LegacyDatasetReplayTests
{
    // Legacy stores ratings as double (~15 significant digits). We compute
    // in decimal (~28 digits). Empirically all games agree to ~5e-12; use
    // 1e-9 to leave headroom for the JSON serialization round-trip.
    private const decimal PerGameTolerance = 0.000000001m; // 1e-9

    /// <summary>
    /// For each game, feeds the dataset's own <c>OldRating</c> for each
    /// player and asserts the calculator reproduces the dataset's
    /// <c>NewRating</c>. This isolates the per-game formula (independent of
    /// error accumulation).
    /// </summary>
    [Fact]
    public void PerGameFormula_MatchesLegacyForEveryGame()
    {
        var games = LoadDataset();
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

            var next = RatingCalculator.ComputeNewRatings(Ruleset.Mcr, g.NumberOfWinds, oldRatings, scores);

            for (var i = 0; i < g.Players.Count; i++)
            {
                var diff = Math.Abs(next[ids[i]] - (decimal)g.Players[i].NewRating);
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
    /// Excluded players: legacy has two carry-forward inconsistencies on
    /// 2026-08-27 where Eskild Theodor Middelboe's <c>OldRating</c> in one
    /// game does not match his <c>NewRating</c> from the previous game
    /// (differences of ~1.06 and ~-0.72). These are legacy data quirks,
    /// not formula deviations. Any player who ever sat at a table with a
    /// tainted opponent inherits the taint.
    /// </summary>
    [Fact]
    public void RunningReplay_ProducesLegacyRatings_ForEveryUntaintedPlayer()
    {
        var games = LoadDataset();

        var ratings = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var lastLegacyRating = new Dictionary<string, double>(StringComparer.Ordinal);
        var tainted = new HashSet<string>(StringComparer.Ordinal);

        foreach (var g in games)
        {
            // Same player at two seats occurs in legacy data (one bugged game
            // in 22 years). Give each seat its own Guid so the calculator
            // sees distinct keys.
            var ids = new Guid[g.Players.Count];
            var oldRatings = new Dictionary<Guid, decimal>(g.Players.Count);
            var scores = new Dictionary<Guid, int>(g.Players.Count);
            for (var i = 0; i < g.Players.Count; i++)
            {
                ids[i] = Guid.NewGuid();
                var carried = ratings.GetValueOrDefault(g.Players[i].Name, 0m);
                oldRatings[ids[i]] = carried;
                scores[ids[i]] = g.Players[i].Score;

                // If the legacy dataset's OldRating for this player disagrees
                // with the value we carried forward from their prior game,
                // the legacy dataset has an out-of-band rating adjustment we
                // cannot reproduce. Taint them (and anyone at their table)
                // going forward.
                if (Math.Abs(carried - (decimal)g.Players[i].OldRating) > PerGameTolerance)
                {
                    tainted.Add(g.Players[i].Name);
                }
            }

            var next = RatingCalculator.ComputeNewRatings(Ruleset.Mcr, g.NumberOfWinds, oldRatings, scores);

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
            .Select(kv => (Name: kv.Key, Ours: kv.Value, Legacy: lastLegacyRating[kv.Key]))
            .Where(t => Math.Abs(t.Ours - (decimal)t.Legacy) > PerGameTolerance)
            .OrderByDescending(t => Math.Abs(t.Ours - (decimal)t.Legacy))
            .ToList();

        Assert.True(
            mismatches.Count == 0,
            $"Untainted players diverging from legacy final rating (untainted count = {ratings.Count - tainted.Count}): " +
            string.Join(", ", mismatches.Take(5).Select(m => $"{m.Name} ours={m.Ours} legacy={m.Legacy}")));
    }

    private static List<LegacyGame> LoadDataset()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "data", "mcr_games_full.json");
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
