namespace Inventory.Application.Features.V1.ApiLogs.DTOs
{
    public sealed record ApiLogListItemDto(
        long Id, string Module, string TraceId, string? Ip, Guid? UserId, string? UserName,
        string Method, string Url, int StatusCode, long DurationMs, DateTime CreatedDate);

    public sealed record ApiLogDetailDto(
        long Id, string Module, string TraceId, string? Ip, Guid? UserId, string? UserName,
        string Method, string Url, int StatusCode, long DurationMs, DateTime CreatedDate,
        string? Request, string? Response, string? UserAgent);
}

namespace Inventory.Application.Features.V1.ApiLogs.Queries
{
    using Inventory.Application.Features.V1.ApiLogs.DTOs;
    using Inventory.Domain.Entities.Sys;

    /// <summary>Tra cứu log API để debug — thường tìm theo traceId lấy từ ProblemDetails.</summary>
    public sealed record SearchApiLogsQuery : PagedQuery, IRequest<PagedResult<ApiLogListItemDto>>
    {
        public string? TraceId { get; init; }
        public Guid? UserId { get; init; }
        public string? Url { get; init; }
        public string? Method { get; init; }
        public int? StatusCode { get; init; }
        public DateTime? From { get; init; }
        public DateTime? To { get; init; }
    }

    public sealed class SearchApiLogsQueryHandler(IUnitOfWork<InventoryDbContext> unitOfWork)
        : IRequestHandler<SearchApiLogsQuery, PagedResult<ApiLogListItemDto>>
    {
        public async Task<PagedResult<ApiLogListItemDto>> Handle(SearchApiLogsQuery q, CancellationToken ct)
        {
            var query = unitOfWork.Repository<SysLogApi>().AsNoTracking();
            if (!string.IsNullOrWhiteSpace(q.TraceId)) query = query.Where(l => l.TraceId == q.TraceId.Trim());
            if (q.UserId is { } userId) query = query.Where(l => l.UserId == userId);
            if (!string.IsNullOrWhiteSpace(q.Url)) query = query.Where(l => l.Url.Contains(q.Url.Trim()));
            if (!string.IsNullOrWhiteSpace(q.Method)) query = query.Where(l => l.Method == q.Method.Trim().ToUpper());
            if (q.StatusCode is { } status) query = query.Where(l => l.StatusCode == status);
            if (q.From is { } from) query = query.Where(l => l.CreatedDate >= from);
            if (q.To is { } to) query = query.Where(l => l.CreatedDate <= to);

            var projected = query.OrderByDescending(l => l.Id).Select(l => new ApiLogListItemDto(
                l.Id, l.Module, l.TraceId, l.Ip, l.UserId, l.UserName, l.Method, l.Url, l.StatusCode, l.DurationMs, l.CreatedDate));
            return await PagedResult<ApiLogListItemDto>.CreateAsync(projected, q.SafePage, q.SafePageSize, ct);
        }
    }

    public sealed record GetApiLogDetailQuery(long Id) : IRequest<ApiLogDetailDto>;

    public sealed class GetApiLogDetailQueryHandler(IUnitOfWork<InventoryDbContext> unitOfWork)
        : IRequestHandler<GetApiLogDetailQuery, ApiLogDetailDto>
    {
        public async Task<ApiLogDetailDto> Handle(GetApiLogDetailQuery request, CancellationToken ct) =>
            await unitOfWork.Repository<SysLogApi>().AsNoTracking()
                .Where(l => l.Id == request.Id)
                .Select(l => new ApiLogDetailDto(l.Id, l.Module, l.TraceId, l.Ip, l.UserId, l.UserName, l.Method, l.Url,
                    l.StatusCode, l.DurationMs, l.CreatedDate, l.Request, l.Response, l.UserAgent))
                .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("ApiLog", request.Id);
    }
}
