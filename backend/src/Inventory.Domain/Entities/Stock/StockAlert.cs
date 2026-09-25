using Inventory.Domain.Common;
using Inventory.Domain.Entities.Catalog;

namespace Inventory.Domain.Entities.Stock;

/// <summary>
/// Cảnh báo tồn dưới ngưỡng cho một (sản phẩm, kho). Job quét định kỳ mở cảnh báo khi tồn rơi xuống dưới
/// MinThreshold và tự đóng khi tồn hồi lại (hoặc bỏ ngưỡng). Mỗi cặp có tối đa MỘT cảnh báo đang mở
/// (unique filtered index) — nhiều instance chạy job cùng lúc cũng không sinh trùng.
/// Do hệ thống tạo nên người tạo = null (<see cref="IExplicitCreator"/>).
/// </summary>
public class StockAlert : BaseEntity, IExplicitCreator
{
    private StockAlert() { }

    public StockAlert(int productId, int warehouseId, decimal quantity, decimal minThreshold, DateTime now)
    {
        ProductId = productId;
        WarehouseId = warehouseId;
        QuantityAtAlert = quantity;
        MinThreshold = minThreshold;
        CreatedDate = now;
    }

    public long Id { get; private set; }
    public int ProductId { get; private set; }
    public Product Product { get; private set; } = null!;
    public int WarehouseId { get; private set; }
    public Warehouse Warehouse { get; private set; } = null!;

    /// <summary>Tồn lúc phát hiện.</summary>
    public decimal QuantityAtAlert { get; private set; }

    public decimal MinThreshold { get; private set; }
    public bool IsResolved { get; private set; }
    public DateTime? ResolvedAt { get; private set; }

    public void Resolve(DateTime now)
    {
        if (IsResolved) return;
        IsResolved = true;
        ResolvedAt = now;
    }
}
