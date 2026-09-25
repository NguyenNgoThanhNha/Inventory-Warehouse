// Báo cáo chỉ đọc, tổng hợp bằng stored procedure (RULES 3.7, 3.10): gom DTO + Queries trong một file.
namespace Inventory.Application.Features.V1.Reports.DTOs
{
    public sealed record KardexRowDto(
        long Id,
        DateTime OccurredAt,
        int DocumentId,
        string DocumentCode,
        DocumentType DocumentType,
        MovementType MovementType,
        int WarehouseId,
        string WarehouseCode,
        decimal InQty,
        decimal OutQty,
        decimal Balance);

    /// <summary>Thẻ kho một sản phẩm: tồn đầu kỳ, tổng nhập/xuất, tồn cuối kỳ và các dòng (running total) của trang.</summary>
    public sealed record KardexDto(
        int ProductId,
        string Sku,
        string ProductName,
        string Unit,
        int? WarehouseId,
        DateOnly From,
        DateOnly To,
        decimal Opening,
        decimal TotalIn,
        decimal TotalOut,
        decimal Closing,
        int TotalCount,
        int Page,
        int PageSize,
        IReadOnlyList<KardexRowDto> Rows);

    public sealed record WarehouseValueDto(int WarehouseId, string Code, string Name, decimal Value, decimal Quantity);

    public sealed record GroupValueDto(int GroupId, string Name, decimal Value);

    public sealed record LowStockItemDto(int ProductId, string Sku, string Name, int WarehouseId, string WarehouseCode, decimal Quantity, decimal MinThreshold);

    public sealed record InOutDayDto(DateOnly Date, decimal InValue, decimal OutValue);

    public sealed record SlowMovingItemDto(int ProductId, string Sku, string Name, decimal Quantity, decimal Value, DateTime? LastOutAt);

    public sealed record DashboardDto(
        DateTime GeneratedAt,
        decimal StockValue,
        int ProductsInStock,
        int LowStockCount,
        int DraftDocuments,
        int PostedToday,
        int SlowMovingDays,
        IReadOnlyList<WarehouseValueDto> ValueByWarehouse,
        IReadOnlyList<GroupValueDto> ValueByGroup,
        IReadOnlyList<LowStockItemDto> TopLowStock,
        IReadOnlyList<InOutDayDto> InOutByDay,
        IReadOnlyList<SlowMovingItemDto> SlowMoving);
}

namespace Inventory.Application.Features.V1.Reports.Queries.GetKardex
{
    using System.Collections;
    using FluentValidation;
    using Inventory.Application.Common.Data;
    using Inventory.Application.Features.V1.Reports.DTOs;
    using Inventory.Domain.Entities.Catalog;
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Sổ nhập – xuất – tồn (kardex) của một sản phẩm, một kho (hoặc mọi kho) trong [from, to] theo ngày địa phương.
    /// Mặc định 30 ngày gần nhất.
    /// </summary>
    public sealed record GetKardexQuery : PagedQuery, IRequest<KardexDto>
    {
        public int ProductId { get; init; }
        public int? WarehouseId { get; init; }
        public DateOnly? From { get; init; }
        public DateOnly? To { get; init; }
    }

    public sealed class GetKardexQueryValidator : AbstractValidator<GetKardexQuery>
    {
        public const int MaxRangeDays = 366;

        public GetKardexQueryValidator()
        {
            RuleFor(x => x.ProductId).GreaterThan(0).WithMessage("Chọn sản phẩm.");
            RuleFor(x => x.From).LessThanOrEqualTo(x => x.To).When(x => x.From is not null && x.To is not null)
                .WithMessage("Ngày bắt đầu phải trước ngày kết thúc.");
            RuleFor(x => x).Must(x => x.From is null || x.To is null || x.To.Value.DayNumber - x.From.Value.DayNumber < MaxRangeDays)
                .WithName("from").WithMessage($"Khoảng thời gian tối đa {MaxRangeDays} ngày.");
        }
    }

    /// <summary>
    /// Running total cần tính trên toàn bộ khoảng rồi mới phân trang → [dbo].[usp_Report_Kardex] dùng
    /// SUM() OVER (ORDER BY ... ROWS UNBOUNDED PRECEDING); LINQ/EF không diễn đạt gọn được.
    /// </summary>
    public sealed class GetKardexQueryHandler(IUnitOfWork<InventoryDbContext> unitOfWork, IOptions<AppOptions> options, TimeProvider clock)
        : IRequestHandler<GetKardexQuery, KardexDto>
    {
        public async Task<KardexDto> Handle(GetKardexQuery request, CancellationToken ct)
        {
            var product = await unitOfWork.Repository<Product>().AsNoTracking().IgnoreQueryFilters() // SP đã xóa mềm vẫn có lịch sử
                              .Where(p => p.Id == request.ProductId)
                              .Select(p => new { p.Sku, p.Name, p.Unit })
                              .FirstOrDefaultAsync(ct)
                          ?? throw new NotFoundException("Product", request.ProductId);

            var zone = options.Value.TimeZone;
            var to = request.To ?? BusinessDate.Today(clock.GetUtcNow().UtcDateTime, zone);
            var from = request.From ?? to.AddDays(-29);

            var ds = unitOfWork.ExecuteStoreProcedureGetMultiTables("[dbo].[usp_Report_Kardex]", new Hashtable
            {
                ["@ProductId"] = request.ProductId,
                ["@WarehouseId"] = request.WarehouseId,
                ["@From"] = BusinessDate.StartUtc(from, zone),
                ["@To"] = BusinessDate.StartUtc(to.AddDays(1), zone),
                ["@Page"] = request.SafePage,
                ["@PageSize"] = request.SafePageSize
            }).ToDataSetSimpleRead();

            var summary = ds.TryRead<SummaryRow>()?.FirstOrDefault() ?? new SummaryRow();
            var rows = (ds.TryRead<Row>() ?? [])
                .Select(r => new KardexRowDto(r.Id, r.OccurredAt, r.DocumentId, r.DocumentCode, r.DocumentType, r.MovementType,
                    r.WarehouseId, r.WarehouseCode, r.InQty, r.OutQty, r.Balance))
                .ToList();

            return new KardexDto(request.ProductId, product.Sku, product.Name, product.Unit, request.WarehouseId, from, to,
                summary.Opening, summary.TotalIn, summary.TotalOut, summary.Closing, summary.TotalCount,
                request.SafePage, request.SafePageSize, rows);
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
}

namespace Inventory.Application.Features.V1.Reports.Queries.GetDashboard
{
    using System.Collections;
    using Inventory.Application.Common.Data;
    using Inventory.Application.Features.V1.Reports.DTOs;
    using Microsoft.Extensions.Options;

    /// <summary>Màn Tổng quan kho (spec §4.4). <see cref="WarehouseId"/> null = toàn hệ thống.</summary>
    public sealed record GetDashboardQuery(int? WarehouseId = null) : IRequest<DashboardDto>;

    /// <summary>6 bảng tổng hợp trong 1 round-trip: [dbo].[usp_Report_Dashboard].</summary>
    public sealed class GetDashboardQueryHandler(
        IUnitOfWork<InventoryDbContext> unitOfWork, ICacheService cache, IOptions<AppOptions> options, TimeProvider clock)
        : IRequestHandler<GetDashboardQuery, DashboardDto>
    {
        public const int ChartDays = 30;
        public const int SlowMovingDays = 30;
        public const int TopItems = 10;

        /// <summary>
        /// SP quét toàn bộ tồn (~170 ms trên 12k mã) mà số liệu tổng quan không cần tức thời → cache ngắn.
        /// FE hiện <see cref="DashboardDto.GeneratedAt"/> để người xem biết số liệu lúc nào.
        /// </summary>
        public static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

        public Task<DashboardDto> Handle(GetDashboardQuery request, CancellationToken ct)
        {
            var zone = options.Value.TimeZone;
            var now = clock.GetUtcNow().UtcDateTime;
            var today = BusinessDate.Today(now, zone);
            return cache.GetOrSetAsync(ConstCacheKey.Dashboard(request.WarehouseId, today),
                _ => Task.FromResult(Load(request.WarehouseId, zone, now, today)), CacheTtl, ct);
        }

        private DashboardDto Load(int? warehouseId, TimeZoneInfo zone, DateTime now, DateOnly today)
        {
            var chartFrom = today.AddDays(-(ChartDays - 1));

            var ds = unitOfWork.ExecuteStoreProcedureGetMultiTables("[dbo].[usp_Report_Dashboard]", new Hashtable
            {
                ["@WarehouseId"] = warehouseId,
                ["@TodayStartUtc"] = BusinessDate.StartUtc(today, zone),
                ["@TodayEndUtc"] = BusinessDate.StartUtc(today.AddDays(1), zone),
                ["@ChartFromUtc"] = BusinessDate.StartUtc(chartFrom, zone),
                ["@SlowBeforeUtc"] = now.AddDays(-SlowMovingDays),
                ["@UtcOffsetMinutes"] = (int)zone.GetUtcOffset(now).TotalMinutes,
                ["@Top"] = TopItems
            }).ToDataSetSimpleRead();

            // Đọc đúng thứ tự bảng SP trả về.
            var kpi = ds.TryRead<KpiRow>()?.FirstOrDefault() ?? new KpiRow();
            var byWarehouse = (ds.TryRead<WarehouseRow>() ?? [])
                .Select(r => new WarehouseValueDto(r.WarehouseId, r.Code, r.Name, r.Value, r.Quantity)).ToList();
            var byGroup = (ds.TryRead<GroupRow>() ?? []).Select(r => new GroupValueDto(r.GroupId, r.Name, r.Value)).ToList();
            var low = (ds.TryRead<LowRow>() ?? [])
                .Select(r => new LowStockItemDto(r.ProductId, r.Sku, r.Name, r.WarehouseId, r.WarehouseCode, r.Quantity, r.MinThreshold))
                .ToList();
            var perDay = (ds.TryRead<DayRow>() ?? []).ToDictionary(r => DateOnly.FromDateTime(r.Day));
            var slow = (ds.TryRead<SlowRow>() ?? [])
                .Select(r => new SlowMovingItemDto(r.ProductId, r.Sku, r.Name, r.Quantity, r.Value, r.LastOutAt)).ToList();

            // SP chỉ trả ngày có phát sinh — lấp đủ 30 ngày cho biểu đồ.
            var inOut = Enumerable.Range(0, ChartDays).Select(i => chartFrom.AddDays(i))
                .Select(d => perDay.TryGetValue(d, out var r) ? new InOutDayDto(d, r.InValue, r.OutValue) : new InOutDayDto(d, 0, 0))
                .ToList();

            return new DashboardDto(now, kpi.StockValue, kpi.ProductsInStock, kpi.LowStockCount, kpi.DraftDocuments,
                kpi.PostedToday, SlowMovingDays, byWarehouse, byGroup, low, inOut, slow);
        }

        private sealed class KpiRow
        {
            public decimal StockValue { get; set; }
            public int ProductsInStock { get; set; }
            public int LowStockCount { get; set; }
            public int DraftDocuments { get; set; }
            public int PostedToday { get; set; }
        }

        private sealed class WarehouseRow
        {
            public int WarehouseId { get; set; }
            public string Code { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public decimal Value { get; set; }
            public decimal Quantity { get; set; }
        }

        private sealed class GroupRow
        {
            public int GroupId { get; set; }
            public string Name { get; set; } = string.Empty;
            public decimal Value { get; set; }
        }

        private sealed class LowRow
        {
            public int ProductId { get; set; }
            public string Sku { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public int WarehouseId { get; set; }
            public string WarehouseCode { get; set; } = string.Empty;
            public decimal Quantity { get; set; }
            public decimal MinThreshold { get; set; }
        }

        private sealed class DayRow
        {
            public DateTime Day { get; set; }
            public decimal InValue { get; set; }
            public decimal OutValue { get; set; }
        }

        private sealed class SlowRow
        {
            public int ProductId { get; set; }
            public string Sku { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public decimal Quantity { get; set; }
            public decimal Value { get; set; }
            public DateTime? LastOutAt { get; set; }
        }
    }
}
