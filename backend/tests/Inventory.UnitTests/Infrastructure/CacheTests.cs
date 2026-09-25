using Inventory.Application.Common.Caching;
using Inventory.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Inventory.UnitTests.Infrastructure;

public class CacheTests
{
    private static DistributedCacheService MemoryCache() =>
        new(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())), NullLogger<DistributedCacheService>.Instance);

    private sealed record Item(int Id, string Name);

    [Fact]
    public async Task Catalog_cache_serves_hits_until_invalidated()
    {
        var catalog = new CatalogCache(MemoryCache());
        var loads = 0;
        Task<IReadOnlyList<Item>> Load(CancellationToken _) => Task.FromResult<IReadOnlyList<Item>>([new Item(++loads, "Ốc vít")]);

        var first = await catalog.GetOrSetAsync("groups", Load);
        var second = await catalog.GetOrSetAsync("groups", Load);
        Assert.Equal(1, loads);
        Assert.Equal(first, second); // round-trips through JSON as an equal record list

        await catalog.InvalidateAsync();
        var third = await catalog.GetOrSetAsync("groups", Load);
        Assert.Equal(2, loads);
        Assert.Equal(2, third[0].Id);
    }

    [Fact]
    public async Task Broken_cache_falls_back_to_the_source_instead_of_failing()
    {
        var redis = Substitute.For<IDistributedCache>();
        redis.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).ThrowsAsync(new TimeoutException("redis down"));
        redis.SetAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<DistributedCacheEntryOptions>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("redis down"));
        var catalog = new CatalogCache(new DistributedCacheService(redis, NullLogger<DistributedCacheService>.Instance));
        var loads = 0;

        var value = await catalog.GetOrSetAsync("warehouses", _ => Task.FromResult(++loads));
        await catalog.InvalidateAsync(); // must not throw either

        Assert.Equal(1, value);
        Assert.Equal(1, loads);
    }

    [Fact]
    public async Task Undeserializable_entry_is_treated_as_a_miss()
    {
        var cache = MemoryCache();
        await cache.SetStringAsync("k", "not json", TimeSpan.FromMinutes(1));

        var value = await cache.GetOrSetAsync("k", _ => Task.FromResult(new Item(7, "x")), TimeSpan.FromMinutes(1));

        Assert.Equal(new Item(7, "x"), value);
    }
}
