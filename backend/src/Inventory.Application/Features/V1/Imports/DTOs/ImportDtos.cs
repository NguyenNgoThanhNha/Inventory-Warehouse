namespace Inventory.Application.Features.V1.Imports.DTOs;

/// <summary>Lỗi của một dòng Excel: <see cref="Row"/> là số dòng như người dùng thấy trong Excel.</summary>
public sealed record RowErrorDto(int Row, string? Column, string Message);

public sealed record ImportProductsResultDto(
    bool DryRun,
    int TotalRows,
    int ValidRows,
    int Created,
    int Updated,
    IReadOnlyList<string> GroupsCreated,
    IReadOnlyList<RowErrorDto> Errors);

/// <summary>Dòng phiếu đọc từ Excel, đã tra ra sản phẩm — FE đổ thẳng vào form lập phiếu.</summary>
public sealed record ParsedLineDto(
    int Row,
    int ProductId,
    string Sku,
    string ProductName,
    string Unit,
    decimal ProductCost,
    decimal Quantity,
    decimal? UnitCost,
    string? Note);

public sealed record ParsedLinesDto(IReadOnlyList<ParsedLineDto> Lines, IReadOnlyList<RowErrorDto> Errors);

public enum ImportTemplateKind
{
    Products = 1,
    DocumentLines = 2
}
