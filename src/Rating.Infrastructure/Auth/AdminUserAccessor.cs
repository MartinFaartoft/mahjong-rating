namespace Rating.Infrastructure.Auth;

/// <summary>
/// Populated once at startup with the id of the seeded admin <c>User</c>. The
/// authentication handler stamps this id into the <c>NameIdentifier</c> claim
/// so downstream code can attribute writes (e.g. <c>Game.CreatedByUserId</c>)
/// without a per-request DB lookup.
/// </summary>
public sealed class AdminUserAccessor
{
    private Guid? _id;

    public Guid AdminUserId => _id ?? throw new InvalidOperationException(
        "Admin user id has not been initialised. Call SeedAsync during startup.");

    internal void Set(Guid id) => _id = id;
}
