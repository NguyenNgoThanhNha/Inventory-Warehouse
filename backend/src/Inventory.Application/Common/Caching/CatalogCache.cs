namespace Inventory.Application.Common.Caching;

public sealed class CatalogCache(ICacheService cache) : ICatalogCache
{
    /// <summary>Dữ liệu danh mục sống tối đa 10 phút kể cả khi không ai sửa.</summary>
    public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    /// <summary>Token sống lâu hơn dữ liệu để key dữ liệu không bị "hồi sinh" bởi token cũ.</summary>
    private static readonly TimeSpan VersionTtl = TimeSpan.FromDays(1);

    public async Task<T> GetOrSetAsync<T>(string name, Func<CancellationToken, Task<T>> factory, CancellationToken ct = default)
    {
        var version = await cache.GetStringAsync(ConstCacheKey.CatalogVersion, ct);
        if (version is null)
        {
            version = NewVersion();
            await cache.SetStringAsync(ConstCacheKey.CatalogVersion, version, VersionTtl, ct);
        }
        return await cache.GetOrSetAsync(ConstCacheKey.Catalog(version, name), factory, Ttl, ct);
    }

    public Task InvalidateAsync(CancellationToken ct = default) =>
        cache.SetStringAsync(ConstCacheKey.CatalogVersion, NewVersion(), VersionTtl, ct);

    private static string NewVersion() => Guid.NewGuid().ToString("N")[..12];
}
