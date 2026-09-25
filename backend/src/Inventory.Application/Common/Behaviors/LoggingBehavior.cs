using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Inventory.Application.Common.Behaviors;

/// <summary>Log thời gian xử lý mỗi request; cảnh báo nếu chậm.</summary>
public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private const int SlowThresholdMs = 500;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var response = await next(cancellationToken);
        sw.Stop();

        if (sw.ElapsedMilliseconds > SlowThresholdMs)
            logger.LogWarning("Slow request {RequestName} took {ElapsedMs} ms", typeof(TRequest).Name, sw.ElapsedMilliseconds);
        else
            logger.LogDebug("Handled {RequestName} in {ElapsedMs} ms", typeof(TRequest).Name, sw.ElapsedMilliseconds);

        return response;
    }
}
