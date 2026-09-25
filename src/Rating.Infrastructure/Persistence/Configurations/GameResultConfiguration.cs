using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rating.Domain.Entities;

namespace Rating.Infrastructure.Persistence.Configurations;

internal sealed class GameResultConfiguration : IEntityTypeConfiguration<GameResult>
{
    public void Configure(EntityTypeBuilder<GameResult> b)
    {
        b.ToTable("game_results");
        b.HasKey(r => new { r.GameId, r.PlayerId });
        b.Property(r => r.Score).IsRequired();
        b.Property(r => r.SeatOrder).IsRequired();

        b.HasOne<Player>()
            .WithMany()
            .HasForeignKey(r => r.PlayerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
