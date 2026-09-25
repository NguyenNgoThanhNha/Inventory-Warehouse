using System.Text.Json;
using Inventory.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Inventory.Infrastructure.Caching;

/// <summary>
/// <see cref="ICacheService"/> trên <see cref="IDistributedCache"/> (Redis hoặc bộ nhớ), giá trị lưu dạng JSON.
/// Cache là tối ưu, không phải nguồn dữ liệu: Redis chết / timeout thì log cảnh báo rồi đọc thẳng DB
/// (RULES 6.2 — bắt lỗi ở hạ tầng để xử lý thật, không nuốt im lặng).
/// </summary>
public sealed class DistributedCacheService(IDistributedCache cache, ILogger<DistributedCacheService> logger) : ICacheService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<T> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan ttl, CancellationToken ct = default)
    {
        var cached = await TryAsync(() => cache.GetAsync(key, ct), key, "read");
        if (cached is { Length: > 0 })
        {
            try
            {
                return JsonSerializer.Deserialize<T>(cached, Json)!;
            }
            catch (JsonException ex)
            {
                // Giá trị cũ không còn khớp kiểu DTO (đổi code giữa hai lần deploy) → coi như miss.
                logger.LogWarning(ex, "Cache entry {CacheKey} could not be deserialized; reloading", key);
            }
        }

        var value = await factory(ct);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, Json);
        await TryAsync(async () =>
        {
            await cache.SetAsync(key, bytes, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl }, ct);
            return true;
        }, key, "write");
        return value;
    }

    public Task<string?> GetStringAsync(string key, CancellationToken ct = default) =>
        TryAsync(() => cache.GetStringAsync(key, ct), key, "read");

    public Task SetStringAsync(string key, string value, TimeSpan ttl, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            await cache.SetStringAsync(key, value, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl }, ct);
            return true;
        }, key, "write");

    private async Task<TResult?> TryAsync<TResult>(Func<Task<TResult?>> action, string key, string operation)
    {
        try
        {
            return await action();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Cache {Operation} failed for {CacheKey}; falling back to the database", operation, key);
            return default;
        }
    }
}
