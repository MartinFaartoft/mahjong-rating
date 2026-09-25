using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rating.Domain.Entities;

namespace Rating.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users");
        b.HasKey(u => u.Id);
        b.Property(u => u.Username).IsRequired().HasMaxLength(64);
        b.HasIndex(u => u.Username).IsUnique();

        b.HasOne<Player>()
            .WithMany()
            .HasForeignKey(u => u.PlayerId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
