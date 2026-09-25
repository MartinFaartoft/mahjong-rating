using Rating.Domain.Enums;

namespace Rating.Application.Dto;

public sealed record RatingDto(Guid PlayerId, string DisplayName, Ruleset Ruleset, decimal Rating);

public sealed record RatingHistoryDto(Guid GameId, Ruleset Ruleset, decimal RatingAfter, DateTimeOffset ComputedAt);
