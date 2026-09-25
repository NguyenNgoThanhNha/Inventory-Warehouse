using Inventory.Domain.Constants;

namespace Inventory.Domain.Exceptions;

/// <summary>Vi phạm quy tắc nghiệp vụ trong domain (map sang HTTP 409).</summary>
public class DomainException(string message) : Exception(message);

/// <summary>Một dòng thiếu hàng: tồn hiện có <see cref="Available"/> nhỏ hơn số cần giảm <see cref="Requested"/>.</summary>
public sealed record StockShortage(int ProductId, string? Sku, int WarehouseId, decimal Available, decimal Requested);

/// <summary>Xuất/chuyển/điều chỉnh làm tồn âm. Liệt kê đủ mọi dòng thiếu để người dùng sửa một lần.</summary>
public sealed class InsufficientStockException(IReadOnlyList<StockShortage> shortages)
    : DomainException(string.Format(ConstMessage.InsufficientStock, string.Join("; ", shortages.Select(Describe))))
{
    public IReadOnlyList<StockShortage> Shortages { get; } = shortages;

    private static string Describe(StockShortage s) =>
        $"{s.Sku ?? $"#{s.ProductId}"} tồn {s.Available:0.###}, cần {s.Requested:0.###}";
}
