namespace Rating.Domain.Entities;

public sealed class GameResult
{
    public Guid GameId { get; init; }
    public Guid PlayerId { get; init; }
    public int Score { get; set; }
    public int SeatOrder { get; set; }
}
