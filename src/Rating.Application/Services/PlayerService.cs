using Rating.Application.Abstractions;
using Rating.Application.Dto;
using Rating.Domain.Entities;

namespace Rating.Application.Services;

public sealed class PlayerService(IPlayerRepository players, IUnitOfWork uow) : IPlayerService
{
    public async Task<PlayerDto> CreateAsync(CreatePlayerRequest request, CancellationToken ct = default)
    {
        var name = (request.DisplayName ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            throw new ArgumentException("DisplayName is required.", nameof(request));
        }

        var player = new Player { Id = Guid.NewGuid(), DisplayName = name };
        players.Add(player);
        await uow.SaveChangesAsync(ct);
        return new PlayerDto(player.Id, player.DisplayName);
    }

    public async Task<IReadOnlyList<PlayerDto>> ListAsync(CancellationToken ct = default)
    {
        var list = await players.ListAsync(ct);
        return list.Select(p => new PlayerDto(p.Id, p.DisplayName)).ToList();
    }

    public async Task<PlayerDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var p = await players.GetAsync(id, ct);
        return p is null ? null : new PlayerDto(p.Id, p.DisplayName);
    }
}
