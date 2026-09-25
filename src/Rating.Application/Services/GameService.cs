using Rating.Application.Abstractions;
using Rating.Application.Dto;
using Rating.Domain.Entities;
using Rating.Domain.Enums;

namespace Rating.Application.Services;

public sealed class GameService(
    IGameRepository games,
    IPlayerRepository players,
    IRatingService ratings,
    IUnitOfWork uow,
    IClock clock,
    ICurrentUser currentUser) : IGameService
{
    public async Task<GameDto> AddAsync(AddGameRequest request, CancellationToken ct = default)
    {
        Validate(request.NumberOfWinds, request.Results);
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("No authenticated user.");
        await EnsurePlayersExistAsync(request.Results.Select(r => r.PlayerId).ToList(), ct);

        var game = new Game
        {
            Id = Guid.NewGuid(),
            Ruleset = request.Ruleset,
            NumberOfWinds = request.NumberOfWinds,
            FinishedAt = request.FinishedAt,
            CreatedAt = clock.UtcNow,
            CreatedByUserId = userId,
        };
        foreach (var r in request.Results)
        {
            game.Results.Add(new GameResult
            {
                GameId = game.Id,
                PlayerId = r.PlayerId,
                Score = r.Score,
                SeatOrder = r.SeatOrder,
            });
        }
        games.Add(game);
        await uow.SaveChangesAsync(ct);

        await ratings.RecomputeFromAsync(game.Ruleset, game.FinishedAt, ct);
        await uow.SaveChangesAsync(ct);

        return ToDto(game);
    }

    public async Task<GameDto> UpdateAsync(Guid id, UpdateGameRequest request, CancellationToken ct = default)
    {
        var game = await games.GetAsync(id, ct)
            ?? throw new KeyNotFoundException($"Game {id} not found.");
        Validate(request.NumberOfWinds, request.Results);
        await EnsurePlayersExistAsync(request.Results.Select(r => r.PlayerId).ToList(), ct);

        var oldFinishedAt = game.FinishedAt;
        game.NumberOfWinds = request.NumberOfWinds;
        game.FinishedAt = request.FinishedAt;
        game.Results.Clear();
        foreach (var r in request.Results)
        {
            game.Results.Add(new GameResult
            {
                GameId = game.Id,
                PlayerId = r.PlayerId,
                Score = r.Score,
                SeatOrder = r.SeatOrder,
            });
        }

        var from = oldFinishedAt < request.FinishedAt ? oldFinishedAt : request.FinishedAt;
        await uow.SaveChangesAsync(ct);
        await ratings.RecomputeFromAsync(game.Ruleset, from, ct);
        await uow.SaveChangesAsync(ct);

        return ToDto(game);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var game = await games.GetAsync(id, ct)
            ?? throw new KeyNotFoundException($"Game {id} not found.");
        var ruleset = game.Ruleset;
        var finishedAt = game.FinishedAt;
        games.Remove(game);
        await uow.SaveChangesAsync(ct);

        await ratings.RecomputeFromAsync(ruleset, finishedAt, ct);
        await uow.SaveChangesAsync(ct);
    }

    public async Task<GameDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var g = await games.GetAsync(id, ct);
        return g is null ? null : ToDto(g);
    }

    public async Task<IReadOnlyList<GameDto>> ListAsync(Ruleset? ruleset, int limit, DateTimeOffset? before, CancellationToken ct = default)
    {
        var list = await games.ListAsync(ruleset, limit, before, ct);
        return list.Select(ToDto).ToList();
    }

    private static void Validate(int winds, IReadOnlyList<GameResultRequest> results)
    {
        if (winds <= 0) throw new ArgumentException("NumberOfWinds must be positive.");
        if (results is null || results.Count < 2) throw new ArgumentException("A game needs at least two participants.");
        if (results.Select(r => r.PlayerId).Distinct().Count() != results.Count)
            throw new ArgumentException("Duplicate players in the same game.");
    }

    private async Task EnsurePlayersExistAsync(IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        var existing = await players.ExistingIdsAsync(ids, ct);
        var missing = ids.Except(existing).ToList();
        if (missing.Count > 0)
        {
            throw new ArgumentException($"Unknown player id(s): {string.Join(", ", missing)}");
        }
    }

    private static GameDto ToDto(Game g) => new(
        g.Id, g.Ruleset, g.NumberOfWinds, g.FinishedAt, g.CreatedAt, g.CreatedByUserId,
        g.Results.OrderBy(r => r.SeatOrder)
            .Select(r => new GameResultDto(r.PlayerId, r.Score, r.SeatOrder))
            .ToList());
}
