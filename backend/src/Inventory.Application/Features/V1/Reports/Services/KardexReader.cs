using System.Collections;
using Inventory.Application.Common.Data;
using Inventory.Application.Features.V1.Reports.DTOs;
using Inventory.Domain.Entities.Catalog;
using Microsoft.Extensions.Options;

namespace Inventory.Application.Features.V1.Reports.Services;

/// <summary>Đọc thẻ kho từ [dbo].[usp_Report_Kardex] — dùng chung cho API và file Excel.</summary>
public interface IKardexReader
{
    /// <param name="from">Ngày địa phương; null = 29 ngày trước <paramref name="to"/>.</param>
    /// <param name="to">Ngày địa phương (tính cả ngày); null = hôm nay.</param>
    Task<KardexDto> ReadAsync(int productId, int? warehouseId, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct);
}

/// <summary>
/// Running total cần tính trên toàn bộ khoảng rồi mới phân trang → SP dùng
/// SUM() OVER (ORDER BY ... ROWS UNBOUNDED PRECEDING); LINQ/EF không diễn đạt gọn được.
/// </summary>
public sealed class KardexReader(IUnitOfWork<InventoryDbContext> unitOfWork, IOptions<AppOptions> options, TimeProvider clock) : IKardexReader
{
    public async Task<KardexDto> ReadAsync(int productId, int? warehouseId, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct)
    {
        var product = await unitOfWork.Repository<Product>().AsNoTracking().IgnoreQueryFilters() // SP đã xóa mềm vẫn có lịch sử
                          .Where(p => p.Id == productId)
                          .Select(p => new { p.Sku, p.Name, p.Unit })
                          .FirstOrDefaultAsync(ct)
                      ?? throw new NotFoundException("Product", productId);

        var zone = options.Value.TimeZone;
        var toDay = to ?? BusinessDate.Today(clock.GetUtcNow().UtcDateTime, zone);
        var fromDay = from ?? toDay.AddDays(-29);

        var ds = unitOfWork.ExecuteStoreProcedureGetMultiTables("[dbo].[usp_Report_Kardex]", new Hashtable
        {
            ["@ProductId"] = productId,
            ["@WarehouseId"] = warehouseId,
            ["@From"] = BusinessDate.StartUtc(fromDay, zone),
            ["@To"] = BusinessDate.StartUtc(toDay.AddDays(1), zone),
            ["@Page"] = page,
            ["@PageSize"] = pageSize
        }).ToDataSetSimpleRead();

        var summary = ds.TryRead<SummaryRow>()?.FirstOrDefault() ?? new SummaryRow();
        var rows = (ds.TryRead<Row>() ?? [])
            .Select(r => new KardexRowDto(r.Id, r.OccurredAt, r.DocumentId, r.DocumentCode, r.DocumentType, r.MovementType,
                r.WarehouseId, r.WarehouseCode, r.InQty, r.OutQty, r.Balance))
            .ToList();

        return new KardexDto(productId, product.Sku, product.Name, product.Unit, warehouseId, fromDay, toDay,
            summary.Opening, summary.TotalIn, summary.TotalOut, summary.Closing, summary.TotalCount, page, pageSize, rows);
    }

    private sealed class SummaryRow
    {
        public int TotalCount { get; set; }
        public decimal Opening { get; set; }
        public decimal TotalIn { get; set; }
        public decimal TotalOut { get; set; }
        public decimal Closing { get; set; }
    }

    private sealed class Row
    {
        public long Id { get; set; }
        public DateTime OccurredAt { get; set; }
        public int DocumentId { get; set; }
        public string DocumentCode { get; set; } = string.Empty;
        public DocumentType DocumentType { get; set; }
        public MovementType MovementType { get; set; }
        public int WarehouseId { get; set; }
        public string WarehouseCode { get; set; } = string.Empty;
        public decimal InQty { get; set; }
        public decimal OutQty { get; set; }
        public decimal Balance { get; set; }
    }
}
