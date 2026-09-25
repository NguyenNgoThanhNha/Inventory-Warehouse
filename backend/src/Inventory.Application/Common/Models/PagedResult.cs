using Microsoft.EntityFrameworkCore;

namespace Inventory.Application.Common.Models;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    public static async Task<PagedResult<T>> CreateAsync(IQueryable<T> query, int page, int pageSize, CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<T>(items, total, page, pageSize);
    }
}

public abstract record PagedQuery
{
    public const int MaxPageSize = 100;

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;

    public int SafePage => Math.Max(1, Page);
    public int SafePageSize => Math.Clamp(PageSize, 1, MaxPageSize);
}

public sealed record UserSummaryDto(Guid Id, string FullName);
