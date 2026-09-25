using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Inventory.Application.Common.Interfaces;
using Inventory.Domain.Entities.Sys;
using Inventory.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Inventory.Infrastructure.Logging;

/// <summary>Cấu hình log API request/response (chuẩn BE §9.2, dựa trên LoggingRequestResponseMiddleware của VAS_CRM_BE).</summary>
public sealed class ApiLoggingOptions
{
    public const string SectionName = "ApiLogging";

    public bool Enabled { get; set; } = true;
    public string Module { get; set; } = "Api";

    /// <summary>GET thành công mặc định không ghi (GET lỗi ≥ 400 luôn ghi).</summary>
    public bool LogGetRequests { get; set; }

    public int MaxBodyLength { get; set; } = 32 * 1024;
    public int RetentionDays { get; set; } = 30;
    public int QueueCapacity { get; set; } = 10_000;
    public string[] ExcludedPaths { get; set; } = ["/health", "/swagger", "/api/v1/api-logs"];

    public string[] SensitiveFields { get; set; } =
        ["password", "newPassword", "currentPassword", "accessToken", "refreshToken", "token"];
}

/// <summary>Hàng đợi trong bộ nhớ: middleware ghi vào, writer nền đọc ra → request không phải chờ DB log.</summary>
public sealed class ApiLogQueue
{
    private readonly Channel<SysLogApi> _channel;

    public ApiLogQueue(IOptions<ApiLoggingOptions> options) =>
        _channel = Channel.CreateBounded<SysLogApi>(new BoundedChannelOptions(Math.Max(100, options.Value.QueueCapacity))
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true
        });

    public bool TryEnqueue(SysLogApi entry) => _channel.Writer.TryWrite(entry);

    public ChannelReader<SysLogApi> Reader => _channel.Reader;
}

/// <summary>Che field nhạy cảm trong JSON và cắt độ dài body.</summary>
public static class ApiLogSanitizer
{
    public const string Masked = "***";
    public const string TruncatedSuffix = "…[truncated]";

    public static string? Sanitize(string? body, string? contentType, ApiLoggingOptions options)
    {
        if (string.IsNullOrEmpty(body)) return null;
        var text = body;

        if (contentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true || LooksLikeJson(body))
            text = MaskJson(body, options.SensitiveFields) ?? body;

        return text.Length > options.MaxBodyLength ? text[..options.MaxBodyLength] + TruncatedSuffix : text;
    }

    public static string? MaskJson(string json, IReadOnlyCollection<string> sensitiveFields)
    {
        try
        {
            var node = JsonNode.Parse(json);
            if (node is null) return json;
            Mask(node, new HashSet<string>(sensitiveFields, StringComparer.OrdinalIgnoreCase));
            return node.ToJsonString(new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        }
        catch (JsonException)
        {
            return null; // không phải JSON hợp lệ → giữ nguyên
        }
    }

    private static void Mask(JsonNode node, HashSet<string> sensitive)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(p => p.Key).ToList())
                {
                    if (sensitive.Contains(key)) obj[key] = Masked;
                    else if (obj[key] is { } child) Mask(child, sensitive);
                }
                break;
            case JsonArray array:
                foreach (var item in array)
                    if (item is not null) Mask(item, sensitive);
                break;
        }
    }

    private static bool LooksLikeJson(string body)
    {
        var trimmed = body.TrimStart();
        return trimmed.StartsWith('{') || trimmed.StartsWith('[');
    }
}

/// <summary>Đọc hàng đợi và ghi Sys_LogApi theo lô (2 giây hoặc 100 bản ghi / lần SaveChanges).</summary>
public sealed class ApiLogWriterService(
    ApiLogQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<ApiLogWriterService> logger) : BackgroundService
{
    private const int BatchSize = 100;
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new List<SysLogApi>(BatchSize);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var flushTimeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                flushTimeout.CancelAfter(FlushInterval);
                while (batch.Count < BatchSize && await queue.Reader.WaitToReadAsync(flushTimeout.Token))
                    while (batch.Count < BatchSize && queue.Reader.TryRead(out var item)) batch.Add(item);
            }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
                // hết FlushInterval → ghi những gì đang có
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await FlushAsync(batch);
        }

        while (queue.Reader.TryRead(out var rest)) batch.Add(rest);
        await FlushAsync(batch); // ghi nốt khi app tắt
    }

    private async Task FlushAsync(List<SysLogApi> batch)
    {
        if (batch.Count == 0) return;
        try
        {
            using var scope = scopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork<InventoryDbContext>>();
            unitOfWork.Repository<SysLogApi>().AddRange(batch);
            await unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Log hỏng không được làm hỏng API: chỉ cảnh báo qua Serilog.
            logger.LogWarning(ex, "Failed to write {Count} API log entries", batch.Count);
        }
        finally
        {
            batch.Clear();
        }
    }
}

/// <summary>Mỗi ngày xóa log API cũ hơn RetentionDays.</summary>
public sealed class ApiLogCleanupService(
    IServiceScopeFactory scopeFactory,
    IOptions<ApiLoggingOptions> options,
    TimeProvider clock,
    ILogger<ApiLogCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork<InventoryDbContext>>();
                var cutoff = clock.GetUtcNow().UtcDateTime.AddDays(-Math.Max(1, options.Value.RetentionDays));
                var deleted = await unitOfWork.Repository<SysLogApi>().Where(l => l.CreatedDate < cutoff).ExecuteDeleteAsync(stoppingToken);
                if (deleted > 0) logger.LogInformation("Deleted {Count} API log entries older than {Cutoff:u}", deleted, cutoff);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "API log cleanup failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
