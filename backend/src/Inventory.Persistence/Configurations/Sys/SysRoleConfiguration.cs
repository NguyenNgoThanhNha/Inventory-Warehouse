using Inventory.Domain.Constants;
using Inventory.Domain.Entities.Sys;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Persistence.Configurations.Sys;

public class SysRoleConfiguration : IEntityTypeConfiguration<SysRole>
{
    public void Configure(EntityTypeBuilder<SysRole> builder)
    {
        builder.ToTable(ConstTable.Role);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(255);
        builder.Ignore(x => x.IsAdmin);
        builder.HasIndex(x => x.Name).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
