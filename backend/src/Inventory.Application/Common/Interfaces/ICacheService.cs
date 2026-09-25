namespace Inventory.Application.Common.Interfaces;

/// <summary>
/// Cache phân tán (Redis khi có cấu hình, bộ nhớ khi không). Lỗi cache không bao giờ làm hỏng request:
/// cache không đọc/ghi được thì gọi thẳng <c>factory</c> (và log cảnh báo).
/// </summary>
public interface ICacheService
{
    Task<T> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan ttl, CancellationToken ct = default);

    Task<string?> GetStringAsync(string key, CancellationToken ct = default);

    Task SetStringAsync(string key, string value, TimeSpan ttl, CancellationToken ct = default);
}

/// <summary>
/// Cache danh mục (sản phẩm, nhóm hàng, kho) — ít đổi, đọc rất nhiều (ô chọn sản phẩm, bộ lọc).
/// Vô hiệu bằng "version token": mọi key có token hiện tại; sửa danh mục → đổi token → toàn bộ key cũ tự mồ côi
/// và hết hạn theo TTL. Không cần quét/xóa từng key (Redis không có xóa theo pattern rẻ).
/// </summary>
public interface ICatalogCache
{
    Task<T> GetOrSetAsync<T>(string name, Func<CancellationToken, Task<T>> factory, CancellationToken ct = default);

    /// <summary>Gọi SAU khi SaveChanges thành công ở mọi lệnh sửa sản phẩm / nhóm hàng / kho.</summary>
    Task InvalidateAsync(CancellationToken ct = default);
}
