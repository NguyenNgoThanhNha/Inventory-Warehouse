using Inventory.Domain.Enums;

namespace Inventory.Domain.Entities.Stock;

/// <summary>
/// Sổ cái tồn kho BẤT BIẾN: mọi thay đổi tồn đều sinh đúng một dòng ở đây (kardex + audit).
/// Chỉ thêm — không sửa, không xóa, nên không kế thừa BaseEntity (không có xóa mềm/Updated*).
/// Người và thời điểm ghi do StockLedger gán tường minh.
/// </summary>
public class StockMovement
{
    private StockMovement() { }

    public StockMovement(int productId, int warehouseId, MovementType type, decimal quantity, decimal balanceAfter,
        StockDocument document, DateTime occurredAt, Guid? createdById, string? createdName)
    {
        ProductId = productId;
        WarehouseId = warehouseId;
        Type = type;
        Quantity = quantity;
        BalanceAfter = balanceAfter;
        Document = document;
        DocumentCode = document.Code;
        OccurredAt = occurredAt;
        CreatedById = createdById;
        CreatedName = createdName;
    }

    public long Id { get; private set; }
    public int ProductId { get; private set; }
    public int WarehouseId { get; private set; }
    public MovementType Type { get; private set; }

    /// <summary>Số lượng có dấu: dương = tăng tồn, âm = giảm tồn.</summary>
    public decimal Quantity { get; private set; }

    /// <summary>Tồn sau khi ghi dòng này (đối chiếu nhanh; kardex vẫn tính lại bằng window function).</summary>
    public decimal BalanceAfter { get; private set; }

    public int DocumentId { get; private set; }
    public StockDocument Document { get; private set; } = null!;
    public string DocumentCode { get; private set; } = null!;
    public DateTime OccurredAt { get; private set; }
    public Guid? CreatedById { get; private set; }
    public string? CreatedName { get; private set; }
}
