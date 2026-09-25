using Inventory.Domain.Entities.Stock;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Persistence.Configurations.Stock;

internal static class StockColumns
{
    /// <summary>Số lượng cho phép lẻ (kg, m...) tới 3 chữ số thập phân.</summary>
    public static PropertyBuilder<decimal> Quantity(this PropertyBuilder<decimal> property) => property.HasPrecision(18, 3);

    public static PropertyBuilder<decimal?> Quantity(this PropertyBuilder<decimal?> property) => property.HasPrecision(18, 3);
}

public class StockLevelConfiguration : IEntityTypeConfiguration<StockLevel>
{
    public void Configure(EntityTypeBuilder<StockLevel> builder)
    {
        // Lớp chặn cuối cùng: dù code có lỗi, DB cũng không cho tồn âm.
        builder.ToTable("StockLevels", t =>
        {
            t.HasCheckConstraint("CK_StockLevels_Quantity_NonNegative", "[Quantity] >= 0");
            t.HasCheckConstraint("CK_StockLevels_MinThreshold_NonNegative", "[MinThreshold] >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Quantity).Quantity();
        builder.Property(x => x.MinThreshold).Quantity();
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Ignore(x => x.IsBelowThreshold);

        builder.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);

        // Một dòng tồn cho mỗi (sản phẩm, kho). Không lọc IsDeleted vì StockLevel không bao giờ bị xóa.
        builder.HasIndex(x => new { x.ProductId, x.WarehouseId }).IsUnique()
            .IncludeProperties(x => new { x.Quantity, x.MinThreshold, x.IsDeleted });
        builder.HasIndex(x => x.WarehouseId).IncludeProperties(x => new { x.ProductId, x.Quantity, x.MinThreshold, x.IsDeleted });
    }
}

public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("StockMovements");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Quantity).Quantity();
        builder.Property(x => x.BalanceAfter).Quantity();
        builder.Property(x => x.DocumentCode).HasColumnType("varchar(20)").IsRequired();
        builder.Property(x => x.CreatedName).HasMaxLength(100);

        builder.HasOne<Domain.Entities.Catalog.Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Domain.Entities.Catalog.Warehouse>().WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Document).WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);

        // Kardex: lọc theo SP + kho, sắp theo thời gian.
        builder.HasIndex(x => new { x.ProductId, x.WarehouseId, x.OccurredAt }).IncludeProperties(x => new { x.Quantity, x.Type });
        builder.HasIndex(x => x.DocumentId);
    }
}

public class StockDocumentConfiguration : IEntityTypeConfiguration<StockDocument>
{
    public void Configure(EntityTypeBuilder<StockDocument> builder)
    {
        builder.ToTable("StockDocuments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasColumnType("varchar(20)").IsRequired();
        builder.Property(x => x.Type).HasConversion<byte>();
        builder.Property(x => x.Status).HasConversion<byte>();
        builder.Property(x => x.Reason).HasConversion<byte?>();
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.Property(x => x.PostedByName).HasMaxLength(100);
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ToWarehouse).WithMany().HasForeignKey(x => x.ToWarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.DocumentId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(x => x.Code).IsUnique().HasFilter("[IsDeleted] = 0");
        // Màn danh sách: lọc theo loại (+ trạng thái), mới nhất trước.
        builder.HasIndex(x => new { x.Type, x.CreatedDate }).IncludeProperties(x => new { x.Status, x.WarehouseId, x.IsDeleted });
    }
}

public class StockDocumentLineConfiguration : IEntityTypeConfiguration<StockDocumentLine>
{
    public void Configure(EntityTypeBuilder<StockDocumentLine> builder)
    {
        builder.ToTable("StockDocumentLines");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Quantity).Quantity();
        builder.Property(x => x.SystemQuantity).Quantity();
        builder.Property(x => x.UnitCost).HasPrecision(18, 2);
        builder.Property(x => x.Note).HasMaxLength(200);
        builder.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.DocumentId, x.ProductId }).IsUnique();
    }
}

public class DocumentSequenceConfiguration : IEntityTypeConfiguration<DocumentSequence>
{
    public void Configure(EntityTypeBuilder<DocumentSequence> builder)
    {
        builder.ToTable("DocumentSequences");
        builder.HasKey(x => new { x.Type, x.Year });
        builder.Property(x => x.Type).HasConversion<byte>();
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("IdempotencyRecords");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Key).HasColumnType("varchar(100)").IsRequired();
        builder.Property(x => x.RequestName).HasColumnType("varchar(100)").IsRequired();
        builder.HasOne(x => x.Document).WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        // Unique: hai request trùng key chạy song song → request sau vi phạm index → được thử lại và đọc kết quả của request trước.
        builder.HasIndex(x => new { x.UserId, x.Key }).IsUnique();
    }
}
