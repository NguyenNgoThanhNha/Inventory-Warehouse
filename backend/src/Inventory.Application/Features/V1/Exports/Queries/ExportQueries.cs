using System.Globalization;
using FluentValidation;
using Inventory.Application.Features.V1.Reports.Services;
using Inventory.Application.Features.V1.Stock.Services;
using Inventory.Domain.Entities.Catalog;
using Inventory.Domain.Entities.Stock;
using Microsoft.Extensions.Options;

namespace Inventory.Application.Features.V1.Exports.Queries;

public static class ExportLimits
{
    /// <summary>
    /// Export là ngoại lệ có chủ đích của RULES 3.6 (không phân trang) — nhưng vẫn có trần để một request không
    /// kéo cả DB lên RAM. Vượt trần → 400 kèm hướng dẫn lọc bớt.
    /// </summary>
    public const int MaxRows = 100_000;
}

/// <summary>Xuất bảng tồn kho ra Excel với ĐÚNG bộ lọc của màn Tồn kho (<see cref="StockSearch"/>).</summary>
public sealed record ExportStockQuery(int? WarehouseId, int? GroupId, string? Search, bool BelowThreshold, StockSort Sort = StockSort.Sku)
    : IRequest<FileDto>;

public sealed class ExportStockQueryHandler(
    IUnitOfWork<InventoryDbContext> unitOfWork, ISpreadsheetService spreadsheet, IOptions<AppOptions> options, TimeProvider clock)
    : IRequestHandler<ExportStockQuery, FileDto>
{
    public async Task<FileDto> Handle(ExportStockQuery request, CancellationToken ct)
    {
        var (products, levels) = StockSearch.Build(unitOfWork, request.WarehouseId, request.GroupId, request.Search, request.BelowThreshold);
        var count = await products.CountAsync(ct);
        if (count > ExportLimits.MaxRows)
            throw new ValidationException("filter", $"Có {count:N0} sản phẩm, tối đa {ExportLimits.MaxRows:N0} dòng mỗi file. Hãy lọc theo kho hoặc nhóm hàng.");

        var warehouses = await unitOfWork.Repository<Warehouse>().AsNoTracking()
            .Where(w => request.WarehouseId == null ? w.IsActive : w.Id == request.WarehouseId)
            .OrderBy(w => w.Code).Select(w => new { w.Id, w.Code }).ToListAsync(ct);

        // 2 query (sản phẩm, dòng tồn) rồi ghép trong bộ nhớ — không N+1, không Include.
        var rows = await StockSearch.Order(products, levels, request.Sort)
            .Select(p => new { p.Id, p.Sku, p.Name, p.Unit, Group = p.Group.Name, p.Cost })
            .ToListAsync(ct);
        var qty = (await levels.Select(s => new { s.ProductId, s.WarehouseId, s.Quantity, s.MinThreshold }).ToListAsync(ct))
            .ToDictionary(s => (s.ProductId, s.WarehouseId));

        var columns = new List<SheetColumn>
        {
            new("SKU", Width: 16), new("Tên sản phẩm", Width: 36), new("ĐVT", Width: 8), new("Nhóm hàng", Width: 22)
        };
        columns.AddRange(warehouses.Select(w => new SheetColumn(w.Code, SheetColumnFormat.Quantity, 12)));
        columns.AddRange([
            new SheetColumn("Tổng", SheetColumnFormat.Quantity, 12), new SheetColumn("Giá vốn", SheetColumnFormat.Money, 14),
            new SheetColumn("Giá trị tồn", SheetColumnFormat.Money, 18), new SheetColumn("Dưới ngưỡng tại", Width: 20)
        ]);

        var data = rows.Select(p =>
        {
            var cells = warehouses.Select(w => qty.GetValueOrDefault((p.Id, w.Id))).ToList();
            var total = cells.Sum(c => c?.Quantity ?? 0);
            var low = warehouses.Where(w => qty.TryGetValue((p.Id, w.Id), out var c) && c.MinThreshold > 0 && c.Quantity < c.MinThreshold)
                .Select(w => w.Code);
            var values = new List<object?> { p.Sku, p.Name, p.Unit, p.Group };
            values.AddRange(cells.Select(c => (object?)(c?.Quantity ?? 0m)));
            values.AddRange([total, p.Cost, total * p.Cost, string.Join(", ", low)]);
            return values.ToArray();
        });

        var now = TimeZoneInfo.ConvertTimeFromUtc(clock.GetUtcNow().UtcDateTime, options.Value.TimeZone);
        var content = spreadsheet.Write([new SheetData("Ton kho", columns, data,
            [$"Tồn kho lúc {now:dd/MM/yyyy HH:mm} · {rows.Count:N0} sản phẩm"])]);
        return new FileDto(content, FileDto.XlsxContentType, $"ton-kho-{now:yyyyMMdd-HHmm}.xlsx");
    }
}

/// <summary>Xuất thẻ kho (toàn bộ khoảng, không phân trang) ra Excel.</summary>
public sealed record ExportKardexQuery(int ProductId, int? WarehouseId, DateOnly? From, DateOnly? To) : IRequest<FileDto>;

public sealed class ExportKardexQueryValidator : AbstractValidator<ExportKardexQuery>
{
    public ExportKardexQueryValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0).WithMessage("Chọn sản phẩm.");
        RuleFor(x => x.From).LessThanOrEqualTo(x => x.To).When(x => x.From is not null && x.To is not null)
            .WithMessage("Ngày bắt đầu phải trước ngày kết thúc.");
    }
}

public sealed class ExportKardexQueryHandler(
    IUnitOfWork<InventoryDbContext> unitOfWork, IKardexReader reader, ISpreadsheetService spreadsheet, IOptions<AppOptions> options)
    : IRequestHandler<ExportKardexQuery, FileDto>
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    public async Task<FileDto> Handle(ExportKardexQuery request, CancellationToken ct)
    {
        var k = await reader.ReadAsync(request.ProductId, request.WarehouseId, request.From, request.To, 1, ExportLimits.MaxRows, ct);
        var warehouseName = request.WarehouseId is { } wid
            ? await unitOfWork.Repository<Warehouse>().AsNoTracking().Where(w => w.Id == wid).Select(w => w.Name).FirstOrDefaultAsync(ct)
            : "Tất cả kho";

        var zone = options.Value.TimeZone; // Excel không có múi giờ → ghi giờ địa phương
        var rows = new List<object?[]> { new object?[] { null, null, "Tồn đầu kỳ", null, null, null, k.Opening } };
        rows.AddRange(k.Rows.Select(r => new object?[]
        {
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(r.OccurredAt, DateTimeKind.Utc), zone), r.DocumentCode, MovementLabel(r.MovementType), r.WarehouseCode,
            r.InQty == 0 ? null : r.InQty, r.OutQty == 0 ? null : r.OutQty, r.Balance
        }));
        rows.Add([null, null, "Cộng kỳ", null, k.TotalIn, k.TotalOut, k.Closing]);

        var content = spreadsheet.Write([new SheetData("The kho",
        [
            new("Thời gian", SheetColumnFormat.DateTime, 18), new("Chứng từ", Width: 18), new("Nghiệp vụ", Width: 14),
            new("Kho", Width: 10), new("Nhập", SheetColumnFormat.Quantity, 12), new("Xuất", SheetColumnFormat.Quantity, 12),
            new("Tồn cuối", SheetColumnFormat.Quantity, 12)
        ], rows,
        [
            $"Thẻ kho {k.Sku} — {k.ProductName} ({k.Unit})",
            $"{warehouseName} · {k.From.ToString("dd/MM/yyyy", Vi)} → {k.To.ToString("dd/MM/yyyy", Vi)}"
        ])]);
        return new FileDto(content, FileDto.XlsxContentType, $"the-kho-{k.Sku}-{k.From:yyyyMMdd}-{k.To:yyyyMMdd}.xlsx");
    }

    private static string MovementLabel(MovementType type) => type switch
    {
        MovementType.In => "Nhập",
        MovementType.Out => "Xuất",
        MovementType.TransferIn => "Chuyển đến",
        MovementType.TransferOut => "Chuyển đi",
        MovementType.Adjust => "Kiểm kê",
        _ => type.ToString()
    };
}
