using Rating.Application.Dto;
using Rating.Domain.Enums;

namespace Rating.Application.Services;

public interface IGameService
{
    Task<GameDto> AddAsync(AddGameRequest request, CancellationToken ct = default);
    Task<GameDto> UpdateAsync(Guid id, UpdateGameRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<GameDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<GameDto>> ListAsync(Ruleset? ruleset, int limit, DateTimeOffset? before, CancellationToken ct = default);
}
