namespace Rating.Domain.Entities;

public sealed class User
{
    public Guid Id { get; init; }
    public required string Username { get; set; }
    public bool IsAdmin { get; set; }
    public Guid? PlayerId { get; set; }
}
