using Inventory.Domain.Constants;
using Inventory.Domain.Entities.Sys;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Persistence.Configurations.Sys;

public class SysRefreshTokenConfiguration : IEntityTypeConfiguration<SysRefreshToken>
{
    public void Configure(EntityTypeBuilder<SysRefreshToken> builder)
    {
        builder.ToTable(ConstTable.RefreshToken);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TokenHash).HasColumnType("varchar(64)").IsRequired();
        builder.Property(x => x.ReplacedByTokenHash).HasColumnType("varchar(64)");
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class SysNotificationConfiguration : IEntityTypeConfiguration<SysNotification>
{
    public void Configure(EntityTypeBuilder<SysNotification> builder)
    {
        builder.ToTable(ConstTable.Notification);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Message).HasMaxLength(500).IsRequired();
        builder.HasIndex(x => new { x.UserId, x.IsRead });
        builder.HasOne<SysAccount>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(x => x.Link).HasMaxLength(200);
    }
}
