using Inventory.Domain.Entities.Catalog;
using Inventory.Domain.Entities.Stock;
using Inventory.Domain.Exceptions;
using Inventory.Persistence.Interceptors;

namespace Inventory.Application.Features.V1.Stock.Services;

/// <summary>Một thay đổi tồn: <see cref="Delta"/> dương = tăng, âm = giảm.</summary>
public sealed record StockChange(int ProductId, int WarehouseId, decimal Delta, MovementType Type);

/// <summary>
/// Service TẬP TRUNG cho mọi thay đổi tồn kho (spec §3.2). Không nơi nào khác được sửa StockLevel.Quantity.
/// - Kiểm tra đủ hàng cho TẤT CẢ dòng trước khi đổi dòng nào (báo đủ các dòng thiếu một lần).
/// - Cập nhật StockLevel (được EF theo dõi kèm RowVersion gốc → UPDATE ... WHERE RowVersion = @original).
/// - Ghi một StockMovement cho mỗi thay đổi (sổ cái bất biến).
/// Không gọi SaveChanges: handler gọi một lần ở cuối, nên cả phiếu (kể cả chuyển kho 2 kho) là một transaction.
/// </summary>
public interface IStockLedger
{
    /// <summary>Đọc (có theo dõi) các dòng tồn theo cặp (sản phẩm, kho). Cặp chưa có dòng thì không có trong kết quả.</summary>
    Task<Dictionary<(int ProductId, int WarehouseId), StockLevel>> LoadAsync(
        IReadOnlyCollection<(int ProductId, int WarehouseId)> keys, CancellationToken ct);

    Task ApplyAsync(StockDocument document, IReadOnlyList<StockChange> changes, CancellationToken ct);
}

public sealed class StockLedger(IUnitOfWork<InventoryDbContext> unitOfWork, IAuditUser auditUser, TimeProvider clock) : IStockLedger
{
    public async Task<Dictionary<(int ProductId, int WarehouseId), StockLevel>> LoadAsync(
        IReadOnlyCollection<(int ProductId, int WarehouseId)> keys, CancellationToken ct)
    {
        if (keys.Count == 0) return [];
        var productIds = keys.Select(k => k.ProductId).Distinct().ToList();
        var warehouseIds = keys.Select(k => k.WarehouseId).Distinct().ToList();

        // Một query cho cả phiếu (không N+1). Lọc thừa theo tích Descartes rồi cắt lại trong bộ nhớ.
        var levels = await unitOfWork.Repository<StockLevel>()
            .Where(s => productIds.Contains(s.ProductId) && warehouseIds.Contains(s.WarehouseId))
            .ToListAsync(ct);

        var wanted = keys.ToHashSet();
        return levels.Where(s => wanted.Contains((s.ProductId, s.WarehouseId)))
            .ToDictionary(s => (s.ProductId, s.WarehouseId));
    }

    public async Task ApplyAsync(StockDocument document, IReadOnlyList<StockChange> changes, CancellationToken ct)
    {
        if (changes.Count == 0) return;
        var levels = await LoadAsync(changes.Select(c => (c.ProductId, c.WarehouseId)).Distinct().ToList(), ct);

        EnsureEnoughStock(changes, levels, out var shortages);
        if (shortages.Count > 0) throw new InsufficientStockException(await WithSkusAsync(shortages, ct));

        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var change in changes)
        {
            var key = (change.ProductId, change.WarehouseId);
            if (!levels.TryGetValue(key, out var level))
            {
                level = new StockLevel(change.ProductId, change.WarehouseId);
                unitOfWork.Repository<StockLevel>().Add(level);
                levels[key] = level;
            }

            level.Adjust(change.Delta);
            unitOfWork.Repository<StockMovement>().Add(new StockMovement(
                change.ProductId, change.WarehouseId, change.Type, change.Delta, level.Quantity,
                document, now, auditUser.UserIdOrNull, auditUser.UserName));
        }
    }

    /// <summary>Cộng dồn theo từng (sản phẩm, kho) để một phiếu có nhiều thay đổi trên cùng dòng vẫn kiểm tra đúng.</summary>
    private static void EnsureEnoughStock(
        IReadOnlyList<StockChange> changes,
        IReadOnlyDictionary<(int, int), StockLevel> levels,
        out List<StockShortage> shortages)
    {
        shortages = [];
        var projected = new Dictionary<(int, int), decimal>();
        foreach (var change in changes)
        {
            var key = (change.ProductId, change.WarehouseId);
            var current = projected.TryGetValue(key, out var p) ? p : levels.GetValueOrDefault(key)?.Quantity ?? 0;
            var next = current + change.Delta;
            if (next < 0) shortages.Add(new StockShortage(change.ProductId, null, change.WarehouseId, current, -change.Delta));
            projected[key] = next;
        }
    }

    private async Task<IReadOnlyList<StockShortage>> WithSkusAsync(List<StockShortage> shortages, CancellationToken ct)
    {
        var ids = shortages.Select(s => s.ProductId).Distinct().ToList();
        var skus = await unitOfWork.Repository<Product>().AsNoTracking().IgnoreQueryFilters() // SP đã xóa mềm vẫn cần mã để báo lỗi
            .Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Sku, ct);
        return shortages.Select(s => s with { Sku = skus.GetValueOrDefault(s.ProductId) }).ToList();
    }
}

/// <summary>Thứ tự của màn Tồn kho / file xuất. Tổng = tổng tồn các kho đang lọc.</summary>
public enum StockSort
{
    Sku = 1,
    Name = 2,
    TotalDesc = 3,
    TotalAsc = 4
}

/// <summary>
/// Bộ lọc + thứ tự dùng chung cho màn Tồn kho và file Excel xuất tồn kho — hai nơi phải ra đúng cùng một tập sản phẩm, cùng thứ tự.
/// </summary>
public static class StockSearch
{
    /// <summary>Luôn chốt bằng SKU (duy nhất) để phân trang ổn định khi nhiều sản phẩm trùng tên / trùng tổng.</summary>
    public static IOrderedQueryable<Product> Order(IQueryable<Product> products, IQueryable<StockLevel> levels, StockSort sort) => sort switch
    {
        StockSort.Name => products.OrderBy(p => p.Name).ThenBy(p => p.Sku),
        StockSort.TotalDesc => products.OrderByDescending(p => levels.Where(s => s.ProductId == p.Id).Sum(s => s.Quantity)).ThenBy(p => p.Sku),
        StockSort.TotalAsc => products.OrderBy(p => levels.Where(s => s.ProductId == p.Id).Sum(s => s.Quantity)).ThenBy(p => p.Sku),
        _ => products.OrderBy(p => p.Sku)
    };

    public static (IQueryable<Product> Products, IQueryable<StockLevel> Levels) Build(
        IUnitOfWork<InventoryDbContext> unitOfWork, int? warehouseId, int? groupId, string? search, bool belowThreshold)
    {
        var levels = unitOfWork.Repository<StockLevel>().AsNoTracking();
        if (warehouseId is { } wid) levels = levels.Where(s => s.WarehouseId == wid);

        var products = unitOfWork.Repository<Product>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var sku = Product.NormalizeSku(term);
            products = products.Where(p => p.Sku.StartsWith(sku) || p.Name.Contains(term));
        }
        if (groupId is { } gid) products = products.Where(p => p.GroupId == gid);
        // Một kho: SP có dòng tồn ở kho đó. Mọi kho: SP đang kinh doanh, hoặc đã ngừng nhưng vẫn còn hàng.
        if (warehouseId is not null) products = products.Where(p => levels.Any(s => s.ProductId == p.Id));
        else products = products.Where(p => p.IsActive || levels.Any(s => s.ProductId == p.Id && s.Quantity > 0));
        if (belowThreshold)
            products = products.Where(p => levels.Any(s => s.ProductId == p.Id && s.MinThreshold > 0 && s.Quantity < s.MinThreshold));
        return (products, levels);
    }
}
