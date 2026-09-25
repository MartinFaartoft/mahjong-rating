using Microsoft.AspNetCore.Mvc;
using Rating.Application.Dto;
using Rating.Application.Services;
using Rating.Domain.Enums;

namespace Rating.Api.Controllers;

[ApiController]
[Route("ratings")]
public sealed class RatingsController(IRatingService ratings) : ControllerBase
{
    [HttpGet("{ruleset}")]
    public Task<IReadOnlyList<RatingDto>> Current(Ruleset ruleset, CancellationToken ct)
        => ratings.GetCurrentAsync(ruleset, ct);

    [HttpGet("{ruleset}/players/{playerId:guid}/history")]
    public Task<IReadOnlyList<RatingHistoryDto>> History(Ruleset ruleset, Guid playerId, CancellationToken ct)
        => ratings.GetHistoryAsync(ruleset, playerId, ct);
}
