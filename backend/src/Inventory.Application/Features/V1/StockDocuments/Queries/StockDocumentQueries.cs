using Inventory.Application.Features.V1.StockDocuments.DTOs;
using Inventory.Application.Features.V1.StockDocuments.Services;
using Inventory.Domain.Entities.Stock;

namespace Inventory.Application.Features.V1.StockDocuments.Queries;

/// <summary>Danh sách phiếu một loại (controller gán <see cref="Type"/> theo route), mới nhất trước.</summary>
public sealed record SearchStockDocumentsQuery : PagedQuery, IRequest<PagedResult<StockDocumentListItemDto>>
{
    public DocumentType Type { get; init; }
    public DocumentStatus? Status { get; init; }

    /// <summary>Kho của phiếu (với chuyển kho: khớp kho xuất hoặc kho nhận).</summary>
    public int? WarehouseId { get; init; }

    /// <summary>Mã phiếu (chứa).</summary>
    public string? Search { get; init; }

    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
}

public sealed class SearchStockDocumentsQueryHandler(IUnitOfWork<InventoryDbContext> unitOfWork)
    : IRequestHandler<SearchStockDocumentsQuery, PagedResult<StockDocumentListItemDto>>
{
    public async Task<PagedResult<StockDocumentListItemDto>> Handle(SearchStockDocumentsQuery request, CancellationToken ct)
    {
        var query = unitOfWork.Repository<StockDocument>().AsNoTracking().Where(d => d.Type == request.Type);
        if (request.Status is { } status) query = query.Where(d => d.Status == status);
        if (request.WarehouseId is { } warehouseId) query = query.Where(d => d.WarehouseId == warehouseId || d.ToWarehouseId == warehouseId);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToUpperInvariant();
            query = query.Where(d => d.Code.Contains(term));
        }
        if (request.From is { } from)
        {
            var start = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(d => d.CreatedDate >= start);
        }
        if (request.To is { } to)
        {
            var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(d => d.CreatedDate < end);
        }

        var projected = query.OrderByDescending(d => d.CreatedDate).ThenByDescending(d => d.Id)
            .Select(d => new StockDocumentListItemDto(
                d.Id, d.Code, d.Type, d.Status, d.Warehouse.Name,
                d.ToWarehouse == null ? null : d.ToWarehouse.Name,
                d.Supplier == null ? null : d.Supplier.Name,
                d.Reason, d.Lines.Count, d.Lines.Sum(l => l.Quantity), d.CreatedDate, d.CreatedName, d.PostedAt));

        return await PagedResult<StockDocumentListItemDto>.CreateAsync(projected, request.SafePage, request.SafePageSize, ct);
    }
}

public sealed record GetStockDocumentQuery(int Id, DocumentType Type) : IRequest<StockDocumentDto>;

public sealed class GetStockDocumentQueryHandler(IStockDocumentReader reader) : IRequestHandler<GetStockDocumentQuery, StockDocumentDto>
{
    public Task<StockDocumentDto> Handle(GetStockDocumentQuery request, CancellationToken ct) =>
        reader.GetAsync(request.Id, request.Type, ct);
}
