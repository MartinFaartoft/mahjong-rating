using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rating.Domain.Entities;

namespace Rating.Infrastructure.Persistence.Configurations;

internal sealed class RatingHistoryEntryConfiguration : IEntityTypeConfiguration<RatingHistoryEntry>
{
    public void Configure(EntityTypeBuilder<RatingHistoryEntry> b)
    {
        b.ToTable("rating_history");
        b.HasKey(e => e.Id);

        b.Property(e => e.Ruleset).HasConversion<string>().HasMaxLength(16).IsRequired();
        // numeric(18,10): comfortable range, exact arithmetic, no floating-point drift.
        b.Property(e => e.RatingAfter).HasColumnType("numeric(18,10)").IsRequired();
        b.Property(e => e.ComputedAt).IsRequired();

        b.HasIndex(e => new { e.PlayerId, e.Ruleset, e.ComputedAt });
        b.HasIndex(e => new { e.GameId, e.PlayerId }).IsUnique();

        b.HasOne<Player>()
            .WithMany()
            .HasForeignKey(e => e.PlayerId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne<Game>()
            .WithMany()
            .HasForeignKey(e => e.GameId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
