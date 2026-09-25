using Inventory.Domain.Common;
using Inventory.Domain.Entities.Catalog;
using Inventory.Domain.Exceptions;

namespace Inventory.Domain.Entities.Stock;

/// <summary>
/// Tồn hiện tại của một sản phẩm tại một kho. <see cref="RowVersion"/> là trái tim concurrency:
/// hai phiếu cùng trừ một dòng thì phiếu lưu sau nhận DbUpdateConcurrencyException.
/// Không bao giờ xóa (kể cả mềm) — tồn về 0 thì dòng vẫn còn để giữ ngưỡng cảnh báo.
/// </summary>
public class StockLevel : BaseEntity
{
    private StockLevel() { }

    public StockLevel(int productId, int warehouseId)
    {
        ProductId = productId;
        WarehouseId = warehouseId;
    }

    public long Id { get; private set; }
    public int ProductId { get; private set; }
    public Product Product { get; private set; } = null!;
    public int WarehouseId { get; private set; }
    public Warehouse Warehouse { get; private set; } = null!;
    public decimal Quantity { get; private set; }

    /// <summary>Ngưỡng tồn tối thiểu (0 = không cảnh báo).</summary>
    public decimal MinThreshold { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public bool IsBelowThreshold => MinThreshold > 0 && Quantity < MinThreshold;

    /// <summary>Cộng/trừ tồn. Không cho âm — lớp chặn trong code (DB còn CHECK constraint làm lớp cuối).</summary>
    public void Adjust(decimal delta, string? sku = null)
    {
        var next = Quantity + delta;
        if (next < 0)
            throw new InsufficientStockException([new StockShortage(ProductId, sku, WarehouseId, Quantity, -delta)]);
        Quantity = next;
    }

    public void SetMinThreshold(decimal value)
    {
        if (value < 0) throw new DomainException("Ngưỡng tồn tối thiểu không được âm.");
        MinThreshold = value;
    }
}
