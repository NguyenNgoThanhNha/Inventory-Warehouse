using Inventory.Domain.Constants;
using Inventory.Domain.Entities.Sys;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Persistence.Configurations.Sys;

public class SysRoleActivityConfiguration : IEntityTypeConfiguration<SysRoleActivity>
{
    public void Configure(EntityTypeBuilder<SysRoleActivity> builder)
    {
        builder.ToTable(ConstTable.RoleActivity);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Description).HasMaxLength(255);
        builder.Ignore(x => x.IsEmpty);
        builder.HasOne(x => x.Role).WithMany(r => r.RoleActivities).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Activity).WithMany().HasForeignKey(x => x.ActivityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.RoleId, x.ActivityId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class SysUserActivityConfiguration : IEntityTypeConfiguration<SysUserActivity>
{
    public void Configure(EntityTypeBuilder<SysUserActivity> builder)
    {
        builder.ToTable(ConstTable.UserActivity);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Description).HasMaxLength(255);
        builder.Ignore(x => x.IsEmpty);
        builder.HasOne(x => x.User).WithMany(u => u.UserActivities).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Activity).WithMany().HasForeignKey(x => x.ActivityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.UserId, x.ActivityId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
