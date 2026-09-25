namespace Inventory.Application.Features.V1.StockDocuments.DTOs;

public sealed record RefDto(int Id, string Name);

/// <summary>Dòng hàng nhập vào khi lập phiếu. Với kiểm kê, <see cref="Quantity"/> là số đếm thực tế.</summary>
public sealed record DocumentLineInput(int ProductId, decimal Quantity, decimal? UnitCost = null, string? Note = null);

public sealed record StockDocumentLineDto(
    long Id,
    int ProductId,
    string Sku,
    string ProductName,
    string Unit,
    decimal Quantity,
    decimal? UnitCost,
    decimal? SystemQuantity,
    decimal? Difference,
    string? Note);

public sealed record StockDocumentDto(
    int Id,
    string Code,
    DocumentType Type,
    DocumentStatus Status,
    RefDto Warehouse,
    RefDto? ToWarehouse,
    RefDto? Supplier,
    IssueReason? Reason,
    string? Note,
    DateTime CreatedDate,
    string? CreatedName,
    DateTime? PostedAt,
    string? PostedByName,
    string RowVersion,
    IReadOnlyList<StockDocumentLineDto> Lines);

public sealed record StockDocumentListItemDto(
    int Id,
    string Code,
    DocumentType Type,
    DocumentStatus Status,
    string WarehouseName,
    string? ToWarehouseName,
    string? SupplierName,
    IssueReason? Reason,
    int LineCount,
    decimal TotalQuantity,
    DateTime CreatedDate,
    string? CreatedName,
    DateTime? PostedAt);
