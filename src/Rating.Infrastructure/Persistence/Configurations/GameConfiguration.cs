using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rating.Domain.Entities;

namespace Rating.Infrastructure.Persistence.Configurations;

internal sealed class GameConfiguration : IEntityTypeConfiguration<Game>
{
    public void Configure(EntityTypeBuilder<Game> b)
    {
        b.ToTable("games");
        b.HasKey(g => g.Id);

        // Store ruleset as string for readability in psql / audit.
        b.Property(g => g.Ruleset).HasConversion<string>().HasMaxLength(16).IsRequired();
        b.Property(g => g.NumberOfWinds).IsRequired();
        b.Property(g => g.FinishedAt).IsRequired();
        b.Property(g => g.CreatedAt).IsRequired();
        b.Property(g => g.CreatedByUserId).IsRequired();

        // Deterministic replay ordering.
        b.HasIndex(g => new { g.Ruleset, g.FinishedAt, g.CreatedAt, g.Id });

        b.HasMany(g => g.Results)
            .WithOne()
            .HasForeignKey(r => r.GameId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne<User>()
            .WithMany()
            .HasForeignKey(g => g.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
