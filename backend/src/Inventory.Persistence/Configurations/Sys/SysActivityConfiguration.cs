using Inventory.Domain.Constants;
using Inventory.Domain.Entities.Sys;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Persistence.Configurations.Sys;

public class SysActivityConfiguration : IEntityTypeConfiguration<SysActivity>
{
    public void Configure(EntityTypeBuilder<SysActivity> builder)
    {
        builder.ToTable(ConstTable.Activity);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasColumnType("varchar(50)").IsRequired();
        builder.Property(x => x.Name).HasMaxLength(255).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.ApplicationName).HasMaxLength(50);
        builder.HasIndex(x => x.Code).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
