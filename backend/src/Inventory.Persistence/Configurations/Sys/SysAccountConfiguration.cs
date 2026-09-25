using Inventory.Domain.Constants;
using Inventory.Domain.Entities.Sys;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Persistence.Configurations.Sys;

public class SysAccountConfiguration : IEntityTypeConfiguration<SysAccount>
{
    public void Configure(EntityTypeBuilder<SysAccount> builder)
    {
        builder.ToTable(ConstTable.Account);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Email).HasColumnType("varchar(256)").IsRequired();
        builder.Property(x => x.FullName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.PasswordHash).HasMaxLength(256).IsRequired();
        builder.Property(x => x.PasswordResetTokenHash).HasColumnType("varchar(64)");
        builder.HasIndex(x => x.Email).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
