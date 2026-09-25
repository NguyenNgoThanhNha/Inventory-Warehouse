namespace Inventory.Application.Features.V1.Stock.DTOs
{
    public sealed record StockCellDto(int WarehouseId, decimal Quantity, decimal MinThreshold, bool IsLow);

    /// <summary>Một dòng của màn Tồn kho: một sản phẩm, tồn từng kho (cột động) và tổng.</summary>
    public sealed record StockRowDto(
        int ProductId, string Sku, string Name, string Unit, string GroupName,
        decimal Total, bool IsLow, IReadOnlyList<StockCellDto> Warehouses);

    public sealed record AvailableStockDto(int ProductId, decimal Quantity);

    public sealed record StockThresholdDto(int ProductId, int WarehouseId, decimal Quantity, decimal MinThreshold, bool IsLow);
}

namespace Inventory.Application.Features.V1.Stock.Queries
{
    using Inventory.Application.Features.V1.Stock.DTOs;
    using Inventory.Application.Features.V1.Stock.Services;
    using Inventory.Domain.Entities.Catalog;
    using Inventory.Domain.Entities.Stock;

    /// <summary>
    /// Bảng tồn kho phân trang phía server (spec §4.1). Phân trang theo SẢN PHẨM, sau đó lấy tồn các kho
    /// của đúng các sản phẩm trong trang bằng một query thứ hai (2 query / trang, không N+1).
    /// </summary>
    public sealed record SearchStockQuery : PagedQuery, IRequest<PagedResult<StockRowDto>>
    {
        public int? WarehouseId { get; init; }
        public int? GroupId { get; init; }

        /// <summary>SKU (bắt đầu bằng) hoặc tên (chứa).</summary>
        public string? Search { get; init; }

        /// <summary>Chỉ sản phẩm có ít nhất một kho dưới ngưỡng tối thiểu.</summary>
        public bool BelowThreshold { get; init; }

        /// <summary><c>?sort=TotalDesc</c> — mặc định theo SKU.</summary>
        public StockSort Sort { get; init; } = StockSort.Sku;
    }

    public sealed class SearchStockQueryHandler(IUnitOfWork<InventoryDbContext> unitOfWork)
        : IRequestHandler<SearchStockQuery, PagedResult<StockRowDto>>
    {
        public async Task<PagedResult<StockRowDto>> Handle(SearchStockQuery request, CancellationToken ct)
        {
            var (products, levels) = StockSearch.Build(unitOfWork, request.WarehouseId, request.GroupId, request.Search, request.BelowThreshold);

            var total = await products.CountAsync(ct);
            var page = await StockSearch.Order(products, levels, request.Sort)
                .Skip((request.SafePage - 1) * request.SafePageSize).Take(request.SafePageSize)
                .Select(p => new { p.Id, p.Sku, p.Name, p.Unit, GroupName = p.Group.Name })
                .ToListAsync(ct);

            var ids = page.Select(p => p.Id).ToList();
            var cells = (await levels.Where(s => ids.Contains(s.ProductId))
                    .Select(s => new { s.ProductId, s.WarehouseId, s.Quantity, s.MinThreshold })
                    .ToListAsync(ct))
                .ToLookup(s => s.ProductId);

            var items = page.Select(p =>
            {
                var row = cells[p.Id]
                    .OrderBy(c => c.WarehouseId)
                    .Select(c => new StockCellDto(c.WarehouseId, c.Quantity, c.MinThreshold, c.MinThreshold > 0 && c.Quantity < c.MinThreshold))
                    .ToList();
                return new StockRowDto(p.Id, p.Sku, p.Name, p.Unit, p.GroupName, row.Sum(c => c.Quantity), row.Any(c => c.IsLow), row);
            }).ToList();

            return new PagedResult<StockRowDto>(items, total, request.SafePage, request.SafePageSize);
        }
    }

    /// <summary>Tồn hiện tại của vài sản phẩm tại một kho — FE gọi khi lập phiếu xuất để hiện "Tồn hiện" từng dòng.</summary>
    public sealed record GetAvailableStockQuery(int WarehouseId, IReadOnlyList<int> ProductIds) : IRequest<IReadOnlyList<AvailableStockDto>>;

    public sealed class GetAvailableStockQueryHandler(IUnitOfWork<InventoryDbContext> unitOfWork)
        : IRequestHandler<GetAvailableStockQuery, IReadOnlyList<AvailableStockDto>>
    {
        public const int MaxProducts = 200;

        public async Task<IReadOnlyList<AvailableStockDto>> Handle(GetAvailableStockQuery request, CancellationToken ct)
        {
            var ids = request.ProductIds.Distinct().Take(MaxProducts).ToList();
            var found = await unitOfWork.Repository<StockLevel>().AsNoTracking()
                .Where(s => s.WarehouseId == request.WarehouseId && ids.Contains(s.ProductId))
                .ToDictionaryAsync(s => s.ProductId, s => s.Quantity, ct);
            return ids.Select(id => new AvailableStockDto(id, found.GetValueOrDefault(id))).ToList();
        }
    }
}

namespace Inventory.Application.Features.V1.Stock.Commands
{
    using FluentValidation;
    using Inventory.Application.Common.Behaviors;
    using Inventory.Application.Features.V1.Stock.DTOs;
    using Inventory.Domain.Entities.Catalog;
    using Inventory.Domain.Entities.Stock;

    /// <summary>Đặt ngưỡng tồn tối thiểu cho một sản phẩm tại một kho (tạo dòng tồn = 0 nếu chưa có).</summary>
    public sealed record SetStockThresholdCommand(int ProductId, int WarehouseId, decimal MinThreshold)
        : IRequest<StockThresholdDto>, IRetryOnConflict;

    public sealed class SetStockThresholdCommandValidator : AbstractValidator<SetStockThresholdCommand>
    {
        public SetStockThresholdCommandValidator(IUnitOfWork<InventoryDbContext> unitOfWork)
        {
            RuleFor(x => x.MinThreshold).GreaterThanOrEqualTo(0).LessThan(1_000_000_000);
            RuleFor(x => x.ProductId)
                .MustAsync((id, ct) => unitOfWork.Repository<Product>().AnyAsync(p => p.Id == id, ct))
                .WithMessage("Sản phẩm không tồn tại.");
            RuleFor(x => x.WarehouseId)
                .MustAsync((id, ct) => unitOfWork.Repository<Warehouse>().AnyAsync(w => w.Id == id, ct))
                .WithMessage("Kho không tồn tại.");
        }
    }

    public sealed class SetStockThresholdCommandHandler(IUnitOfWork<InventoryDbContext> unitOfWork)
        : IRequestHandler<SetStockThresholdCommand, StockThresholdDto>
    {
        public async Task<StockThresholdDto> Handle(SetStockThresholdCommand request, CancellationToken ct)
        {
            var set = unitOfWork.Repository<StockLevel>();
            var level = await set.FirstOrDefaultAsync(s => s.ProductId == request.ProductId && s.WarehouseId == request.WarehouseId, ct);
            if (level is null)
            {
                level = new StockLevel(request.ProductId, request.WarehouseId);
                set.Add(level);
            }

            level.SetMinThreshold(request.MinThreshold);
            await unitOfWork.SaveChangesAsync(ct);
            return new StockThresholdDto(level.ProductId, level.WarehouseId, level.Quantity, level.MinThreshold, level.IsBelowThreshold);
        }
    }
}
