namespace Rating.Application.Abstractions;

/// <summary>
/// Persists any pending repository changes atomically.
/// </summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct = default);
}
