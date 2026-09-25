using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rating.Application.Dto;
using Rating.Application.Services;
using Rating.Infrastructure.Auth;

namespace Rating.Api.Controllers;

[ApiController]
[Route("players")]
public sealed class PlayersController(IPlayerService players) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<PlayerDto>> List(CancellationToken ct) => players.ListAsync(ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PlayerDto>> Get(Guid id, CancellationToken ct)
        => await players.GetAsync(id, ct) is { } p ? Ok(p) : NotFound();

    [HttpPost]
    [Authorize(Policy = BasicAuthenticationDefaults.AdminPolicy)]
    public async Task<ActionResult<PlayerDto>> Create(CreatePlayerRequest request, CancellationToken ct)
    {
        var dto = await players.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }
}
