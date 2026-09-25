using Rating.Application.Dto;

namespace Rating.Application.Services;

public interface IPlayerService
{
    Task<PlayerDto> CreateAsync(CreatePlayerRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<PlayerDto>> ListAsync(CancellationToken ct = default);
    Task<PlayerDto?> GetAsync(Guid id, CancellationToken ct = default);
}
