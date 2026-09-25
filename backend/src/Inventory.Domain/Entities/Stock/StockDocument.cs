using Inventory.Domain.Common;
using Inventory.Domain.Constants;
using Inventory.Domain.Entities.Catalog;
using Inventory.Domain.Enums;
using Inventory.Domain.Exceptions;

namespace Inventory.Domain.Entities.Stock;

/// <summary>
/// Chứng từ kho dùng chung cho 4 loại phiếu (nhập / xuất / chuyển / kiểm kê) — một bảng, phân biệt bằng <see cref="Type"/>.
/// Vòng đời: Draft → Posted (tồn thay đổi, qua StockLedger) hoặc Draft → Cancelled.
/// </summary>
public class StockDocument : BaseEntity
{
    private readonly List<StockDocumentLine> _lines = [];

    private StockDocument() { }

    public int Id { get; private set; }
    public string Code { get; private set; } = null!;
    public DocumentType Type { get; private set; }
    public DocumentStatus Status { get; private set; }

    /// <summary>Kho của phiếu; với chuyển kho là kho xuất.</summary>
    public int WarehouseId { get; private set; }
    public Warehouse Warehouse { get; private set; } = null!;

    /// <summary>Chỉ có ở phiếu chuyển kho: kho nhận.</summary>
    public int? ToWarehouseId { get; private set; }
    public Warehouse? ToWarehouse { get; private set; }

    /// <summary>Chỉ có ở phiếu nhập.</summary>
    public int? SupplierId { get; private set; }
    public Supplier? Supplier { get; private set; }

    /// <summary>Chỉ có ở phiếu xuất.</summary>
    public IssueReason? Reason { get; private set; }

    public string? Note { get; private set; }
    public DateTime? PostedAt { get; private set; }
    public Guid? PostedById { get; private set; }
    public string? PostedByName { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyCollection<StockDocumentLine> Lines => _lines;

    public static StockDocument NewReceipt(string code, int warehouseId, int supplierId, string? note) =>
        new()
        {
            Code = code, Type = DocumentType.GoodsReceipt, Status = DocumentStatus.Draft,
            WarehouseId = warehouseId, SupplierId = supplierId, Note = Clean(note)
        };

    public static StockDocument NewIssue(string code, int warehouseId, IssueReason reason, string? note) =>
        new()
        {
            Code = code, Type = DocumentType.GoodsIssue, Status = DocumentStatus.Draft,
            WarehouseId = warehouseId, Reason = reason, Note = Clean(note)
        };

    public static StockDocument NewTransfer(string code, int fromWarehouseId, int toWarehouseId, string? note)
    {
        if (fromWarehouseId == toWarehouseId) throw new DomainException("Kho nhận phải khác kho xuất.");
        return new StockDocument
        {
            Code = code, Type = DocumentType.Transfer, Status = DocumentStatus.Draft,
            WarehouseId = fromWarehouseId, ToWarehouseId = toWarehouseId, Note = Clean(note)
        };
    }

    public static StockDocument NewStockTake(string code, int warehouseId, string? note) =>
        new()
        {
            Code = code, Type = DocumentType.StockTake, Status = DocumentStatus.Draft,
            WarehouseId = warehouseId, Note = Clean(note)
        };

    /// <summary>Thêm dòng. Với kiểm kê, <paramref name="quantity"/> là số đếm thực tế (được phép = 0).</summary>
    public void AddLine(int productId, decimal quantity, decimal? unitCost = null, string? note = null)
    {
        EnsureDraft();
        if (_lines.Any(l => l.ProductId == productId)) throw new DomainException($"Sản phẩm #{productId} bị lặp trong phiếu.");
        if (Type == DocumentType.StockTake ? quantity < 0 : quantity <= 0) throw new DomainException("Số lượng không hợp lệ.");
        _lines.Add(new StockDocumentLine(productId, quantity, Type == DocumentType.GoodsReceipt ? unitCost : null, Clean(note)));
    }

    /// <summary>
    /// Sửa phiếu nháp: đổi thông tin chung (chỉ các trường thuộc loại phiếu này) và thay TOÀN BỘ dòng hàng.
    /// Dòng cũ bị xóa hẳn (là phần con của phiếu); phiếu đã ghi sổ / đã hủy thì không sửa được.
    /// </summary>
    public void UpdateDraft(int warehouseId, int? toWarehouseId, int? supplierId, IssueReason? reason, string? note)
    {
        EnsureDraft();
        if (Type == DocumentType.Transfer && toWarehouseId == warehouseId) throw new DomainException("Kho nhận phải khác kho xuất.");
        WarehouseId = warehouseId;
        ToWarehouseId = Type == DocumentType.Transfer ? toWarehouseId : null;
        SupplierId = Type == DocumentType.GoodsReceipt ? supplierId : null;
        Reason = Type == DocumentType.GoodsIssue ? reason : null;
        Note = Clean(note);
        _lines.Clear();
    }

    public void MarkPosted(DateTime now, Guid? userId, string? userName)
    {
        EnsureDraft();
        if (_lines.Count == 0) throw new DomainException("Phiếu chưa có dòng hàng.");
        Status = DocumentStatus.Posted;
        PostedAt = now;
        PostedById = userId;
        PostedByName = userName;
    }

    public void Cancel()
    {
        EnsureDraft();
        Status = DocumentStatus.Cancelled;
    }

    public static string ActivityOf(DocumentType type) => type switch
    {
        DocumentType.GoodsReceipt => ConstActivity.GoodsReceipt,
        DocumentType.GoodsIssue => ConstActivity.GoodsIssue,
        DocumentType.Transfer => ConstActivity.Transfer,
        DocumentType.StockTake => ConstActivity.StockTake,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    public static string PrefixOf(DocumentType type) => type switch
    {
        DocumentType.GoodsReceipt => ConstDocumentPrefix.GoodsReceipt,
        DocumentType.GoodsIssue => ConstDocumentPrefix.GoodsIssue,
        DocumentType.Transfer => ConstDocumentPrefix.Transfer,
        DocumentType.StockTake => ConstDocumentPrefix.StockTake,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private void EnsureDraft()
    {
        if (Status != DocumentStatus.Draft) throw new DomainException(string.Format(ConstMessage.DocumentNotDraft, Code));
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// Dòng hàng của phiếu. Là phần con của aggregate StockDocument (sống/chết theo phiếu) nên không kế thừa BaseEntity.
/// </summary>
public class StockDocumentLine
{
    private StockDocumentLine() { }

    internal StockDocumentLine(int productId, decimal quantity, decimal? unitCost, string? note)
    {
        ProductId = productId;
        Quantity = quantity;
        UnitCost = unitCost;
        Note = note;
    }

    public long Id { get; private set; }
    public int DocumentId { get; private set; }
    public int ProductId { get; private set; }
    public Product Product { get; private set; } = null!;

    /// <summary>Nhập/xuất/chuyển: số lượng (&gt; 0). Kiểm kê: số đếm thực tế (≥ 0).</summary>
    public decimal Quantity { get; private set; }

    /// <summary>Đơn giá nhập (chỉ phiếu nhập).</summary>
    public decimal? UnitCost { get; private set; }

    /// <summary>Kiểm kê: tồn sổ sách tại thời điểm ghi sổ (chụp lại để biết chênh lệch).</summary>
    public decimal? SystemQuantity { get; private set; }

    public string? Note { get; private set; }

    public void RecordSystemQuantity(decimal systemQuantity) => SystemQuantity = systemQuantity;
}

/// <summary>Bộ đếm số chứng từ theo (loại, năm). Có RowVersion: hai phiếu cùng lấy số thì một phiếu phải thử lại.</summary>
public class DocumentSequence
{
    private DocumentSequence() { }

    public DocumentSequence(DocumentType type, int year)
    {
        Type = type;
        Year = year;
    }

    public DocumentType Type { get; private set; }
    public int Year { get; private set; }
    public int LastNumber { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public string Next()
    {
        LastNumber++;
        return $"{StockDocument.PrefixOf(Type)}-{Year}-{LastNumber:D5}";
    }
}

/// <summary>Idempotency-Key đã xử lý: request lặp lại (double-submit, retry mạng) trả về phiếu đã tạo thay vì tạo phiếu mới.</summary>
public class IdempotencyRecord
{
    private IdempotencyRecord() { }

    public IdempotencyRecord(Guid userId, string key, string requestName, StockDocument document, DateTime createdAt)
    {
        UserId = userId;
        Key = key;
        RequestName = requestName;
        Document = document;
        CreatedAt = createdAt;
    }

    public long Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Key { get; private set; } = null!;
    public string RequestName { get; private set; } = null!;
    public int DocumentId { get; private set; }
    public StockDocument Document { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
}
