using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rating.Domain.Entities;
using Rating.Infrastructure.Persistence;

namespace Rating.Infrastructure.Auth;

/// <summary>
/// Ensures a <see cref="User"/> row exists for the configured admin username
/// and publishes its id via <see cref="AdminUserAccessor"/>. Runs once at
/// application startup, after migrations.
/// </summary>
public static class AdminUserSeeder
{
    public static async Task SeedAsync(
        AppDbContext db,
        IOptions<AdminOptions> options,
        AdminUserAccessor accessor,
        ILogger logger,
        CancellationToken ct = default)
    {
        var admin = options.Value;
        if (string.IsNullOrWhiteSpace(admin.Username))
        {
            throw new InvalidOperationException(
                "Admin:Username is not configured. Set Admin__Username in the environment.");
        }

        var existing = await db.Users.FirstOrDefaultAsync(u => u.Username == admin.Username, ct);
        if (existing is null)
        {
            existing = new User { Id = Guid.NewGuid(), Username = admin.Username, IsAdmin = true };
            db.Users.Add(existing);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Seeded admin user {Username} ({UserId}).", existing.Username, existing.Id);
        }
        else if (!existing.IsAdmin)
        {
            existing.IsAdmin = true;
            await db.SaveChangesAsync(ct);
        }

        accessor.Set(existing.Id);
    }
}
