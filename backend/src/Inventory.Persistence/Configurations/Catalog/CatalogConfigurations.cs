using Inventory.Domain.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Persistence.Configurations.Catalog;

public class ProductGroupConfiguration : IEntityTypeConfiguration<ProductGroup>
{
    public void Configure(EntityTypeBuilder<ProductGroup> builder)
    {
        builder.ToTable("ProductGroups");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.Name).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Sku).HasColumnType("varchar(50)").IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Unit).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Cost).HasPrecision(18, 2);
        builder.Property(x => x.Price).HasPrecision(18, 2);
        builder.Property(x => x.ImageUrl).HasMaxLength(500);
        builder.HasOne(x => x.Group).WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.Sku).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => x.GroupId).IncludeProperties(x => new { x.IsDeleted });
        builder.HasIndex(x => x.Name).IncludeProperties(x => new { x.IsDeleted });
    }
}

public class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.ToTable("Warehouses");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasColumnType("varchar(20)").IsRequired();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Address).HasMaxLength(300);
        builder.HasIndex(x => x.Code).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("Suppliers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Phone).HasColumnType("varchar(20)");
        builder.Property(x => x.Email).HasMaxLength(200);
        builder.Property(x => x.Address).HasMaxLength(300);
        builder.HasIndex(x => x.Name).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
