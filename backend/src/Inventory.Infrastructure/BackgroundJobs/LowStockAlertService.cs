using Inventory.Application.Features.V1.StockAlerts.Commands.ScanLowStock;
using Inventory.Application.Features.V1.StockAlerts.Services;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Inventory.Infrastructure.BackgroundJobs;

/// <summary>Định kỳ gửi <see cref="ScanLowStockCommand"/>: mở / đóng cảnh báo tồn thấp và thông báo cho quản lý kho.</summary>
public sealed class LowStockAlertService(
    IServiceScopeFactory scopeFactory,
    IOptions<StockAlertOptions> options,
    ILogger<LowStockAlertService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value;
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(0, opts.InitialDelaySeconds)), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(30, opts.ScanIntervalSeconds)));
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope(); // handler dùng DbContext (Scoped) — RULES 9.2
                var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ScanLowStockCommand(), stoppingToken);
                if (result.Opened > 0 || result.Resolved > 0)
                    logger.LogInformation("Low-stock scan: {Opened} opened, {Resolved} resolved, {StillOpen} open, {Notified} notifications",
                        result.Opened, result.Resolved, result.StillOpen, result.Notified);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Lần quét hỏng không được làm chết job: log lỗi, chờ chu kỳ sau (RULES 6.2).
                logger.LogError(ex, "Low-stock scan failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
