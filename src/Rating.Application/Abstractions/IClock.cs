namespace Rating.Application.Abstractions;

/// <summary>
/// Abstracts <see cref="DateTimeOffset"/>.<see cref="DateTimeOffset.UtcNow"/> so
/// services stay deterministic under test.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
