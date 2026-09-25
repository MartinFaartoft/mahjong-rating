using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rating.Application.Dto;
using Rating.Application.Services;
using Rating.Domain.Enums;
using Rating.Infrastructure.Auth;

namespace Rating.Api.Controllers;

[ApiController]
[Route("games")]
public sealed class GamesController(IGameService games) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<GameDto>> List(
        [FromQuery] Ruleset? ruleset,
        [FromQuery] int limit = 50,
        [FromQuery] DateTimeOffset? before = null,
        CancellationToken ct = default)
        => games.ListAsync(ruleset, limit, before, ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GameDto>> Get(Guid id, CancellationToken ct)
        => await games.GetAsync(id, ct) is { } g ? Ok(g) : NotFound();

    [HttpPost]
    [Authorize(Policy = BasicAuthenticationDefaults.AdminPolicy)]
    public async Task<ActionResult<GameDto>> Create(AddGameRequest request, CancellationToken ct)
    {
        var dto = await games.AddAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = BasicAuthenticationDefaults.AdminPolicy)]
    public Task<GameDto> Update(Guid id, UpdateGameRequest request, CancellationToken ct)
        => games.UpdateAsync(id, request, ct);

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = BasicAuthenticationDefaults.AdminPolicy)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await games.DeleteAsync(id, ct);
        return NoContent();
    }
}
