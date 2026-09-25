using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rating.Domain.Entities;

namespace Rating.Infrastructure.Persistence.Configurations;

internal sealed class PlayerConfiguration : IEntityTypeConfiguration<Player>
{
    public void Configure(EntityTypeBuilder<Player> b)
    {
        b.ToTable("players");
        b.HasKey(p => p.Id);
        b.Property(p => p.DisplayName).IsRequired().HasMaxLength(128);
        b.HasIndex(p => p.DisplayName).IsUnique();
    }
}
