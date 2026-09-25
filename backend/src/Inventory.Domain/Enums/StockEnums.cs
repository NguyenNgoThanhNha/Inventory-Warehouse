namespace Inventory.Domain.Enums;

public enum DocumentType
{
    GoodsReceipt = 1,
    GoodsIssue = 2,
    Transfer = 3,
    StockTake = 4
}

/// <summary>Draft → Posted (đã ghi sổ, tồn đã đổi) hoặc Draft → Cancelled. Posted là trạng thái cuối.</summary>
public enum DocumentStatus
{
    Draft = 1,
    Posted = 2,
    Cancelled = 3
}

public enum IssueReason
{
    Sale = 1,
    Disposal = 2,
    Production = 3,
    Other = 9
}

/// <summary>Loại dòng sổ cái. Quantity của StockMovement mang dấu: nhập dương, xuất âm.</summary>
public enum MovementType
{
    In = 1,
    Out = 2,
    TransferIn = 3,
    TransferOut = 4,
    Adjust = 5
}
