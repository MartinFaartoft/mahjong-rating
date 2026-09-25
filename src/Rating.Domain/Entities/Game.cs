using Rating.Domain.Enums;

namespace Rating.Domain.Entities;

public sealed class Game
{
    public Guid Id { get; init; }
    public Ruleset Ruleset { get; init; }
    public int NumberOfWinds { get; set; }
    public DateTimeOffset FinishedAt { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
    public Guid CreatedByUserId { get; init; }
    public List<GameResult> Results { get; init; } = new();
}
