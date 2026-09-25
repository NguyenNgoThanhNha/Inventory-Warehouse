using System.Linq.Expressions;
using Inventory.Domain.Common;
using Inventory.Domain.Entities.Sys;
using Inventory.Domain.Entities.Catalog;
using Inventory.Domain.Entities.Stock;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Persistence;

public class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
{
    // Phân quyền 6 bảng
    public DbSet<SysAccount> SysAccounts => Set<SysAccount>();
    public DbSet<SysRole> SysRoles => Set<SysRole>();
    public DbSet<SysActivity> SysActivities => Set<SysActivity>();
    public DbSet<SysUserRole> SysUserRoles => Set<SysUserRole>();
    public DbSet<SysRoleActivity> SysRoleActivities => Set<SysRoleActivity>();
    public DbSet<SysUserActivity> SysUserActivities => Set<SysUserActivity>();

    // Hệ thống
    public DbSet<SysRefreshToken> SysRefreshTokens => Set<SysRefreshToken>();
    public DbSet<SysNotification> SysNotifications => Set<SysNotification>();
    public DbSet<SysLogApi> SysLogApis => Set<SysLogApi>();

    // Danh mục
    public DbSet<ProductGroup> ProductGroups => Set<ProductGroup>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();

    // Nghiệp vụ kho
    public DbSet<StockLevel> StockLevels => Set<StockLevel>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<StockDocument> StockDocuments => Set<StockDocument>();
    public DbSet<StockDocumentLine> StockDocumentLines => Set<StockDocumentLine>();
    public DbSet<DocumentSequence> DocumentSequences => Set<DocumentSequence>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InventoryDbContext).Assembly);
        ApplyBaseEntityConventions(modelBuilder);
    }

    /// <summary>Mọi BaseEntity: global query filter !IsDeleted + kiểu cột audit thống nhất.</summary>
    private static void ApplyBaseEntityConventions(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(t => typeof(BaseEntity).IsAssignableFrom(t.ClrType) && t.BaseType is null))
        {
            var builder = modelBuilder.Entity(entityType.ClrType);
            builder.Property(nameof(BaseEntity.CreatedName)).HasMaxLength(100);
            builder.Property(nameof(BaseEntity.Updater)).HasMaxLength(100);
            builder.HasIndex(nameof(BaseEntity.CreatedDate));

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var notDeleted = Expression.Lambda(
                Expression.Not(Expression.Property(parameter, nameof(BaseEntity.IsDeleted))), parameter);
            builder.HasQueryFilter(notDeleted);
        }
    }
}
