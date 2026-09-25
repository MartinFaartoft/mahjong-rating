using Rating.Domain.Enums;

namespace Rating.Application.Dto;

public sealed record GameDto(
    Guid Id,
    Ruleset Ruleset,
    int NumberOfWinds,
    DateTimeOffset FinishedAt,
    DateTimeOffset CreatedAt,
    Guid CreatedByUserId,
    IReadOnlyList<GameResultDto> Results);

public sealed record GameResultDto(Guid PlayerId, int Score, int SeatOrder);

public sealed record GameResultRequest(Guid PlayerId, int Score, int SeatOrder);

public sealed record AddGameRequest(
    Ruleset Ruleset,
    int NumberOfWinds,
    DateTimeOffset FinishedAt,
    IReadOnlyList<GameResultRequest> Results);

public sealed record UpdateGameRequest(
    int NumberOfWinds,
    DateTimeOffset FinishedAt,
    IReadOnlyList<GameResultRequest> Results);
