using Rating.Domain.Enums;

namespace Rating.Domain.Entities;

public sealed class RatingHistoryEntry
{
    public Guid Id { get; init; }
    public Guid PlayerId { get; init; }
    public Ruleset Ruleset { get; init; }
    public Guid GameId { get; init; }
    public decimal RatingAfter { get; init; }
    public DateTimeOffset ComputedAt { get; init; }
}
