using Inventory.Domain.Constants;
using Inventory.Domain.Entities.Sys;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Persistence.Configurations.Sys;

public class SysLogApiConfiguration : IEntityTypeConfiguration<SysLogApi>
{
    public void Configure(EntityTypeBuilder<SysLogApi> builder)
    {
        builder.ToTable(ConstTable.LogApi);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Module).HasMaxLength(100).IsRequired();
        builder.Property(x => x.TraceId).HasColumnType("varchar(100)").IsRequired();
        builder.Property(x => x.Ip).HasColumnType("varchar(50)");
        builder.Property(x => x.UserName).HasMaxLength(256);
        builder.Property(x => x.Method).HasColumnType("varchar(10)").IsRequired();
        builder.Property(x => x.Url).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.UserAgent).HasMaxLength(500);

        builder.HasIndex(x => x.CreatedDate);
        builder.HasIndex(x => x.TraceId);
        builder.HasIndex(x => new { x.UserId, x.CreatedDate });
        builder.HasIndex(x => new { x.StatusCode, x.CreatedDate });
    }
}
