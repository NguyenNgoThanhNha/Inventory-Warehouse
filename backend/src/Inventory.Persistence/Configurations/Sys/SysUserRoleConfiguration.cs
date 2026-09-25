using Inventory.Domain.Constants;
using Inventory.Domain.Entities.Sys;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Persistence.Configurations.Sys;

public class SysUserRoleConfiguration : IEntityTypeConfiguration<SysUserRole>
{
    public void Configure(EntityTypeBuilder<SysUserRole> builder)
    {
        builder.ToTable(ConstTable.UserRole);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Description).HasMaxLength(255);
        builder.HasOne(x => x.User).WithMany(u => u.UserRoles).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Role).WithMany(r => r.UserRoles).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.UserId, x.RoleId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
