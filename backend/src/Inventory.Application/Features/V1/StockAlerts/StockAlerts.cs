namespace Inventory.Application.Features.V1.StockAlerts.DTOs
{
    public sealed record StockAlertDto(
        long Id,
        int ProductId,
        string Sku,
        string ProductName,
        int WarehouseId,
        string WarehouseCode,
        decimal QuantityAtAlert,
        decimal? CurrentQuantity,
        decimal MinThreshold,
        DateTime CreatedAt,
        bool IsResolved,
        DateTime? ResolvedAt);

    public sealed record ScanLowStockResultDto(int Opened, int Resolved, int StillOpen, int Notified);
}

namespace Inventory.Application.Features.V1.StockAlerts.Services
{
    public sealed class StockAlertOptions
    {
        public const string SectionName = "StockAlerts";

        /// <summary>Chu kỳ job quét (giây). Tối thiểu 30.</summary>
        public int ScanIntervalSeconds { get; set; } = 300;

        /// <summary>Chờ bao lâu sau khi khởi động mới quét lần đầu (để migrate/seed xong).</summary>
        public int InitialDelaySeconds { get; set; } = 20;
    }
}

namespace Inventory.Application.Features.V1.StockAlerts.Commands.ScanLowStock
{
    using Inventory.Application.Common.Behaviors;
    using Inventory.Application.Features.V1.StockAlerts.DTOs;
    using Inventory.Domain.Entities.Catalog;
    using Inventory.Domain.Entities.Stock;
    using Inventory.Domain.Entities.Sys;

    /// <summary>
    /// Job quét tồn thấp (spec §3.4). Tồn &lt; ngưỡng mà chưa có cảnh báo mở → mở cảnh báo; cảnh báo mở mà tồn đã hồi
    /// (hoặc bỏ ngưỡng) → đóng. Có cảnh báo MỚI thì gửi MỘT thông báo tóm tắt cho mỗi kho tới người có WAREHOUSE:U
    /// (không gửi từng mặt hàng để khỏi ngập thông báo). Idempotent: chạy lại ngay không sinh gì thêm.
    /// </summary>
    public sealed record ScanLowStockCommand : IRequest<ScanLowStockResultDto>, IRetryOnConflict;

    public sealed class ScanLowStockCommandHandler(IUnitOfWork<InventoryDbContext> unitOfWork, TimeProvider clock)
        : IRequestHandler<ScanLowStockCommand, ScanLowStockResultDto>
    {
        public async Task<ScanLowStockResultDto> Handle(ScanLowStockCommand request, CancellationToken ct)
        {
            var now = clock.GetUtcNow().UtcDateTime;
            var low = await unitOfWork.Repository<StockLevel>().AsNoTracking()
                .Where(s => s.MinThreshold > 0 && s.Quantity < s.MinThreshold)
                .Select(s => new { s.ProductId, s.WarehouseId, s.Quantity, s.MinThreshold })
                .ToListAsync(ct);
            var open = await unitOfWork.Repository<StockAlert>().Where(a => !a.IsResolved).ToListAsync(ct);

            var lowKeys = low.Select(l => (l.ProductId, l.WarehouseId)).ToHashSet();
            var openKeys = open.Select(a => (a.ProductId, a.WarehouseId)).ToHashSet();

            var opened = low.Where(l => !openKeys.Contains((l.ProductId, l.WarehouseId)))
                .Select(l => new StockAlert(l.ProductId, l.WarehouseId, l.Quantity, l.MinThreshold, now))
                .ToList();
            unitOfWork.Repository<StockAlert>().AddRange(opened);

            var resolved = open.Where(a => !lowKeys.Contains((a.ProductId, a.WarehouseId))).ToList();
            resolved.ForEach(a => a.Resolve(now));

            var notified = opened.Count > 0 ? await NotifyAsync(opened, ct) : 0;
            await unitOfWork.SaveChangesAsync(ct);
            return new ScanLowStockResultDto(opened.Count, resolved.Count, open.Count - resolved.Count + opened.Count, notified);
        }

        /// <summary>Một thông báo / kho / người nhận. Người nhận = tài khoản active là Admin hoặc có WAREHOUSE:U (role hoặc quyền riêng).</summary>
        private async Task<int> NotifyAsync(List<StockAlert> opened, CancellationToken ct)
        {
            var recipients = await unitOfWork.Repository<SysAccount>().AsNoTracking()
                .Where(u => u.IsActive && (
                    u.UserRoles.Any(ur => ur.Role.RoleType == ConstRole.AdminRoleType
                                          || ur.Role.RoleActivities.Any(ra => ra.Activity.Code == ConstActivity.Warehouse && ra.U))
                    || u.UserActivities.Any(ua => ua.Activity.Code == ConstActivity.Warehouse && ua.U)))
                .Select(u => u.Id)
                .ToListAsync(ct);
            if (recipients.Count == 0) return 0;

            var warehouseIds = opened.Select(a => a.WarehouseId).Distinct().ToList();
            var names = await unitOfWork.Repository<Warehouse>().AsNoTracking()
                .Where(w => warehouseIds.Contains(w.Id)).ToDictionaryAsync(w => w.Id, w => w.Name, ct);

            var notifications = opened.GroupBy(a => a.WarehouseId)
                .SelectMany(g => recipients.Select(userId => new SysNotification
                {
                    UserId = userId,
                    Message = $"{g.Count()} mặt hàng vừa xuống dưới ngưỡng tồn tối thiểu tại {names.GetValueOrDefault(g.Key, $"kho #{g.Key}")}.",
                    Link = $"/stock?belowThreshold=true&warehouseId={g.Key}"
                }))
                .ToList();
            unitOfWork.Repository<SysNotification>().AddRange(notifications);
            return notifications.Count;
        }
    }
}

namespace Inventory.Application.Features.V1.StockAlerts.Queries
{
    using Inventory.Application.Features.V1.StockAlerts.DTOs;
    using Inventory.Domain.Entities.Stock;

    public sealed record SearchStockAlertsQuery : PagedQuery, IRequest<PagedResult<StockAlertDto>>
    {
        /// <summary>Mặc định chỉ cảnh báo đang mở.</summary>
        public bool IsResolved { get; init; }

        public int? WarehouseId { get; init; }
    }

    public sealed class SearchStockAlertsQueryHandler(IUnitOfWork<InventoryDbContext> unitOfWork)
        : IRequestHandler<SearchStockAlertsQuery, PagedResult<StockAlertDto>>
    {
        public Task<PagedResult<StockAlertDto>> Handle(SearchStockAlertsQuery request, CancellationToken ct)
        {
            var alerts = unitOfWork.Repository<StockAlert>().AsNoTracking().Where(a => a.IsResolved == request.IsResolved);
            if (request.WarehouseId is { } warehouseId) alerts = alerts.Where(a => a.WarehouseId == warehouseId);
            var levels = unitOfWork.Repository<StockLevel>().AsNoTracking();

            var projected = alerts
                .OrderByDescending(a => request.IsResolved ? a.ResolvedAt : a.CreatedDate).ThenByDescending(a => a.Id)
                .Select(a => new StockAlertDto(
                    a.Id, a.ProductId, a.Product.Sku, a.Product.Name, a.WarehouseId, a.Warehouse.Code,
                    a.QuantityAtAlert,
                    levels.Where(s => s.ProductId == a.ProductId && s.WarehouseId == a.WarehouseId).Select(s => (decimal?)s.Quantity).FirstOrDefault(),
                    a.MinThreshold, a.CreatedDate, a.IsResolved, a.ResolvedAt));

            return PagedResult<StockAlertDto>.CreateAsync(projected, request.SafePage, request.SafePageSize, ct);
        }
    }
}
