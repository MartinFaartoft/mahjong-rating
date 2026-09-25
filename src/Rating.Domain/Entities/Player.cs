namespace Rating.Domain.Entities;

public sealed class Player
{
    public Guid Id { get; init; }
    public required string DisplayName { get; set; }
}
