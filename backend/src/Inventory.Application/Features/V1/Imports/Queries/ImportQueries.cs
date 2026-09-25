using FluentValidation;
using Inventory.Application.Features.V1.Imports.DTOs;
using Inventory.Application.Features.V1.Imports.Services;
using Inventory.Application.Features.V1.StockDocuments.Services;
using Inventory.Domain.Entities.Catalog;
using Inventory.Domain.Entities.Stock;

namespace Inventory.Application.Features.V1.Imports.Queries;

/// <summary>
/// Đọc dòng hàng cho một phiếu từ Excel (SKU, Số lượng, Đơn giá, Ghi chú) → tra sản phẩm, báo lỗi từng dòng.
/// KHÔNG ghi gì: FE đổ kết quả vào form lập phiếu để người dùng xem lại rồi mới lưu như bình thường.
/// Quyền: được lập loại phiếu này (C) — kiểm tra trong handler vì loại phiếu là tham số.
/// </summary>
public sealed record ParseDocumentLinesQuery(Stream Content, string FileName, long Size, DocumentType Type) : IRequest<ParsedLinesDto>;

public sealed class ParseDocumentLinesQueryValidator : AbstractValidator<ParseDocumentLinesQuery>
{
    public ParseDocumentLinesQueryValidator()
    {
        this.AddFileRules(x => x.FileName, x => x.Size);
        RuleFor(x => x.Type).IsInEnum();
    }
}

public sealed class ParseDocumentLinesQueryHandler(
    IUnitOfWork<InventoryDbContext> unitOfWork, ISpreadsheetService spreadsheet, ICurrentUser currentUser)
    : IRequestHandler<ParseDocumentLinesQuery, ParsedLinesDto>
{
    public async Task<ParsedLinesDto> Handle(ParseDocumentLinesQuery request, CancellationToken ct)
    {
        if (!await currentUser.HasPermissionAsync(StockDocument.ActivityOf(request.Type), ActivityType.Create, ct))
            throw new ForbiddenException();

        var sheet = spreadsheet.Read(request.Content, StockDocumentRules.MaxLines);
        ImportColumns.EnsurePresent(sheet, ImportColumns.LinesRequired);

        var skus = sheet.Rows.Select(r => r.Get(ImportColumns.Sku)).OfType<string>().Select(Product.NormalizeSku).Distinct().ToList();
        var products = await unitOfWork.Repository<Product>().AsNoTracking()
            .Where(p => skus.Contains(p.Sku))
            .Select(p => new { p.Id, p.Sku, p.Name, p.Unit, p.Cost, p.IsActive })
            .ToDictionaryAsync(p => p.Sku, ct);

        var isStockTake = request.Type == DocumentType.StockTake;
        var errors = new RowErrors();
        var lines = new List<ParsedLineDto>();
        var firstRowOfSku = new Dictionary<string, int>();

        foreach (var row in sheet.Rows)
        {
            var n = row.RowNumber;
            var raw = row.Get(ImportColumns.Sku);
            var sku = raw is null ? null : Product.NormalizeSku(raw);
            var product = sku is not null && products.TryGetValue(sku, out var p) ? p : null;

            if (sku is null) errors.Add(n, ImportColumns.Sku, "Thiếu SKU.");
            else if (product is null) errors.Add(n, ImportColumns.Sku, $"Không tìm thấy sản phẩm {sku}.");
            else if (!product.IsActive) errors.Add(n, ImportColumns.Sku, $"{sku} đã ngừng kinh doanh.");
            else if (firstRowOfSku.TryGetValue(sku, out var first)) errors.Add(n, ImportColumns.Sku, $"{sku} trùng với dòng {first}.");
            else firstRowOfSku[sku] = n;

            var qtyOk = row.TryGetDecimal(ImportColumns.Quantity, out var qty)
                        && (isStockTake ? qty >= 0 : qty > 0) && qty <= StockDocumentRules.MaxQuantity;
            if (!qtyOk) errors.Add(n, ImportColumns.Quantity, isStockTake ? "Số lượng đếm phải ≥ 0." : "Số lượng phải > 0.");

            decimal? unitCost = null;
            if (request.Type == DocumentType.GoodsReceipt && row.Get(ImportColumns.UnitCost) is not null)
            {
                if (row.TryGetDecimal(ImportColumns.UnitCost, out var c) && c >= 0) unitCost = c;
                else errors.Add(n, ImportColumns.UnitCost, "Đơn giá phải là số ≥ 0.");
            }

            var note = row.Get(ImportColumns.Note);
            if (note is { Length: > 200 }) errors.Add(n, ImportColumns.Note, "Ghi chú tối đa 200 ký tự.");

            if (!errors.HasErrors(n))
                lines.Add(new ParsedLineDto(n, product!.Id, product.Sku, product.Name, product.Unit, product.Cost, qty, unitCost, note));
        }

        return new ParsedLinesDto(lines, errors.All.OrderBy(e => e.Row).ToList());
    }
}

/// <summary>File Excel mẫu (sheet dữ liệu có dòng ví dụ + sheet hướng dẫn).</summary>
public sealed record GetImportTemplateQuery(ImportTemplateKind Kind) : IRequest<FileDto>;

public sealed class GetImportTemplateQueryHandler(ISpreadsheetService spreadsheet) : IRequestHandler<GetImportTemplateQuery, FileDto>
{
    public Task<FileDto> Handle(GetImportTemplateQuery request, CancellationToken ct)
    {
        var (fileName, data, guide) = request.Kind switch
        {
            ImportTemplateKind.Products => ("mau-import-san-pham.xlsx",
                new SheetData("San pham",
                [
                    new(ImportColumns.Sku, Width: 16), new(ImportColumns.Name, Width: 36), new(ImportColumns.Unit, Width: 10),
                    new(ImportColumns.Group, Width: 24), new(ImportColumns.Cost, SheetColumnFormat.Money),
                    new(ImportColumns.Price, SheetColumnFormat.Money), new(ImportColumns.IsActive, Width: 16)
                ],
                [
                    ["P901", "Ốc vít M6 (mẫu)", "cái", "Ốc vít & bu lông", 500m, 800m, "có"],
                    ["P902", "Bản lề inox (mẫu)", "cái", "Phụ kiện cửa", 35000m, 52000m, null]
                ]),
                new[]
                {
                    "SKU đã có → cập nhật; SKU chưa có → thêm mới. Nhóm hàng chưa có sẽ được tạo.",
                    "Bắt buộc: SKU, Tên sản phẩm, Đơn vị, Nhóm hàng. Giá để trống = 0.",
                    "Đang kinh doanh: có / không (trống = giữ nguyên, sản phẩm mới mặc định là có).",
                    $"Tối đa {ImportLimits.MaxProductRows:N0} dòng, {ImportLimits.MaxFileBytes / 1024 / 1024}MB. Dòng lỗi được bỏ qua và báo lại."
                }),
            ImportTemplateKind.DocumentLines => ("mau-dong-phieu.xlsx",
                new SheetData("Dong phieu",
                [
                    new(ImportColumns.Sku, Width: 16), new(ImportColumns.Quantity, SheetColumnFormat.Quantity),
                    new(ImportColumns.UnitCost, SheetColumnFormat.Money), new(ImportColumns.Note, Width: 30)
                ],
                [
                    ["P001", 30m, null, null],
                    ["P002", 10m, null, "giao gấp"]
                ]),
                new[]
                {
                    "Bắt buộc: SKU, Số lượng. Kiểm kê: Số lượng là số đếm thực tế (được phép = 0).",
                    "Đơn giá chỉ dùng cho phiếu nhập (trống = giá vốn hiện tại).",
                    $"Tối đa {StockDocumentRules.MaxLines} dòng mỗi phiếu."
                }),
            _ => throw new ValidationException("kind", "Loại file mẫu không hợp lệ.")
        };

        var content = spreadsheet.Write([data, new SheetData("Huong dan", [new SheetColumn("Hướng dẫn", Width: 100)], guide.Select(g => new object?[] { g }))]);
        return Task.FromResult(new FileDto(content, FileDto.XlsxContentType, fileName));
    }
}
