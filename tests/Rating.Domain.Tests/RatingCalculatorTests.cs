using Rating.Domain.Enums;
using Rating.Domain.Services;

namespace Rating.Domain.Tests;

public class RatingCalculatorTests
{
    private static Guid P(int n) => new($"00000000-0000-0000-0000-{n:D12}");

    [Fact]
    public void Mcr_4Wind_4NewPlayers_MatchesLegacyFormula()
    {
        // 4-wind MCR: gl = 4/4 = 1, damping = 40*gl + 1 = 41 (matches legacy
        // mahjongdk impl, verified against the historical dataset).
        // All zero old ratings, scores 40/-40/0/0 -> diff = 0, deltas =
        // 40/41, -40/41, 0, 0.
        var old = new Dictionary<Guid, decimal>
        {
            [P(1)] = 0m, [P(2)] = 0m, [P(3)] = 0m, [P(4)] = 0m,
        };
        var scores = new Dictionary<Guid, int>
        {
            [P(1)] = 40, [P(2)] = -40, [P(3)] = 0, [P(4)] = 0,
        };

        var result = RatingCalculator.ComputeNewRatings(Ruleset.Mcr, 4, old, scores);

        Assert.Equal(40m / 41m, result[P(1)]);
        Assert.Equal(-40m / 41m, result[P(2)]);
        Assert.Equal(0m, result[P(3)]);
        Assert.Equal(0m, result[P(4)]);
    }

    [Fact]
    public void Riichi_1Wind_UsesRiichiGameLengthFactor()
    {
        // Riichi gl = 2/winds. 1 wind -> gl=2, damp = 40*2 + 1 = 81.
        // 2 players, both old=0, scores 40/-40. diff = 0.
        // deltas = (40*2)/81, (-40*2)/81 = 80/81, -80/81.
        var old = new Dictionary<Guid, decimal> { [P(1)] = 0m, [P(2)] = 0m };
        var scores = new Dictionary<Guid, int> { [P(1)] = 40, [P(2)] = -40 };

        var result = RatingCalculator.ComputeNewRatings(Ruleset.Riichi, 1, old, scores);

        Assert.Equal(80m / 81m, result[P(1)]);
        Assert.Equal(-80m / 81m, result[P(2)]);
    }

    [Fact]
    public void GameDifficulty_IsAverageOfOldRatings()
    {
        // 4-wind MCR, gl=1, damp=41. old = [100, 0, 0, 0] -> diff = 25.
        // All scores 0.
        // p1: 100 + (0 + 25 - 100)/41 = 100 - 75/41.
        // p2: 0   + (0 + 25 -   0)/41 =        25/41.
        var old = new Dictionary<Guid, decimal>
        {
            [P(1)] = 100m, [P(2)] = 0m, [P(3)] = 0m, [P(4)] = 0m,
        };
        var scores = new Dictionary<Guid, int>
        {
            [P(1)] = 0, [P(2)] = 0, [P(3)] = 0, [P(4)] = 0,
        };

        var result = RatingCalculator.ComputeNewRatings(Ruleset.Mcr, 4, old, scores);

        Assert.Equal(100m - 75m / 41m, result[P(1)]);
        Assert.Equal(25m / 41m, result[P(2)]);
    }

    [Fact]
    public void DecimalPrecision_LongReplayHasNoDrift()
    {
        // Simulate 10,000 games for two players with alternating scores.
        // Because we compute in decimal without intermediate rounding, replaying
        // via ComputeNewRatings should reach a deterministic value with no drift
        // between runs of the same input.
        var p1 = P(1); var p2 = P(2);
        var ratings = new Dictionary<Guid, decimal> { [p1] = 0m, [p2] = 0m };

        for (var i = 0; i < 10_000; i++)
        {
            var scores = new Dictionary<Guid, int>
            {
                [p1] = i % 2 == 0 ? 30 : -30,
                [p2] = i % 2 == 0 ? -30 : 30,
            };
            var next = RatingCalculator.ComputeNewRatings(Ruleset.Mcr, 4, ratings, scores);
            ratings[p1] = next[p1];
            ratings[p2] = next[p2];
        }

        // Symmetry: opposite-sign scores + identical starting ratings -> sum stays 0.
        Assert.Equal(0m, ratings[p1] + ratings[p2]);
    }

    [Fact]
    public void ThrowsWhenWindsNotPositive()
    {
        var old = new Dictionary<Guid, decimal> { [P(1)] = 0m };
        var scores = new Dictionary<Guid, int> { [P(1)] = 0 };
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RatingCalculator.ComputeNewRatings(Ruleset.Mcr, 0, old, scores));
    }

    [Fact]
    public void ThrowsWhenPlayerSetsMismatch()
    {
        var old = new Dictionary<Guid, decimal> { [P(1)] = 0m, [P(2)] = 0m };
        var scores = new Dictionary<Guid, int> { [P(1)] = 0, [P(3)] = 0 };
        Assert.Throws<ArgumentException>(
            () => RatingCalculator.ComputeNewRatings(Ruleset.Mcr, 4, old, scores));
    }
}
