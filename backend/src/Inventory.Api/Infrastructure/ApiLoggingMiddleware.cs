using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using Inventory.Domain.Entities.Sys;
using Inventory.Infrastructure.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IO;
using Serilog.Context;

namespace Inventory.Api.Infrastructure;

/// <summary>
/// Ghi request/response API vào Sys_LogApi để debug (chuẩn BE §9.2) — phiên bản cải tiến của
/// LoggingRequestResponseMiddleware (VAS_CRM_BE): không chặn request (đẩy vào Channel), che dữ liệu nhạy cảm,
/// cắt body, có TraceId/DurationMs/Ip, lỗi của phần log không làm hỏng API.
/// Đặt ĐẦU pipeline (bọc ngoài UseExceptionHandler) để thấy cả response lỗi.
/// </summary>
public sealed class ApiLoggingMiddleware(
    RequestDelegate next,
    IOptionsMonitor<ApiLoggingOptions> options,
    ApiLogQueue queue,
    TimeProvider clock,
    ILogger<ApiLoggingMiddleware> logger)
{
    private static readonly RecyclableMemoryStreamManager StreamManager = new();

    public async Task InvokeAsync(HttpContext context)
    {
        var opts = options.CurrentValue;
        var path = context.Request.Path.Value ?? string.Empty;
        if (!opts.Enabled || opts.ExcludedPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await next(context);
            return;
        }

        var sw = Stopwatch.StartNew();
        var requestBody = await ReadRequestBodyAsync(context.Request);

        var originalBody = context.Response.Body;
        await using var responseBuffer = StreamManager.GetStream();
        context.Response.Body = responseBuffer;
        try
        {
            await next(context);
        }
        finally
        {
            sw.Stop();
            responseBuffer.Position = 0;
            string? responseBody = null;
            try
            {
                if (ShouldLog(context, opts)) responseBody = await ReadResponseBodyAsync(context.Response, responseBuffer);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not read response body for API log");
            }

            responseBuffer.Position = 0;
            await responseBuffer.CopyToAsync(originalBody);
            context.Response.Body = originalBody;

            if (ShouldLog(context, opts)) Enqueue(context, opts, requestBody, responseBody, sw.ElapsedMilliseconds);
        }
    }

    private static bool ShouldLog(HttpContext context, ApiLoggingOptions opts) =>
        !HttpMethods.IsGet(context.Request.Method) || opts.LogGetRequests || context.Response.StatusCode >= 400;

    private static async Task<string?> ReadRequestBodyAsync(HttpRequest request)
    {
        if (request.ContentLength is 0 || !request.Body.CanRead) return null;
        if (request.HasFormContentType && request.ContentType?.Contains("multipart", StringComparison.OrdinalIgnoreCase) == true)
            return "[multipart/form-data skipped]";

        request.EnableBuffering();
        using var reader = new StreamReader(request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        request.Body.Position = 0;
        return body;
    }

    private static async Task<string?> ReadResponseBodyAsync(HttpResponse response, Stream buffer)
    {
        var contentType = response.ContentType ?? string.Empty;
        if (buffer.Length == 0) return null;
        if (!contentType.Contains("json", StringComparison.OrdinalIgnoreCase) && !contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
            return $"[binary skipped: {contentType}, {buffer.Length} bytes]";

        using var reader = new StreamReader(buffer, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    private void Enqueue(HttpContext context, ApiLoggingOptions opts, string? requestBody, string? responseBody, long elapsedMs)
    {
        try
        {
            var user = context.User;
            var entry = new SysLogApi
            {
                Module = opts.Module,
                TraceId = GlobalExceptionHandler.TraceIdOf(context),
                Ip = context.Connection.RemoteIpAddress?.ToString(),
                UserId = Guid.TryParse(user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) ? id : null,
                UserName = user.FindFirst(JwtRegisteredClaimNames.Email)?.Value,
                Method = context.Request.Method,
                Url = Truncate($"{context.Request.Path}{context.Request.QueryString}", 1000),
                StatusCode = context.Response.StatusCode,
                DurationMs = elapsedMs,
                Request = ApiLogSanitizer.Sanitize(requestBody, context.Request.ContentType, opts),
                Response = ApiLogSanitizer.Sanitize(responseBody, context.Response.ContentType, opts),
                UserAgent = Truncate(context.Request.Headers.UserAgent.ToString(), 500),
                CreatedDate = clock.GetUtcNow().UtcDateTime
            };
            if (!queue.TryEnqueue(entry)) logger.LogWarning("API log queue rejected entry {TraceId}", entry.TraceId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to build API log entry");
        }
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}

public static class ApiLoggingExtensions
{
    public static IApplicationBuilder UseApiLogging(this IApplicationBuilder app) => app.UseMiddleware<ApiLoggingMiddleware>();

    /// <summary>Đẩy UserId vào LogContext để mọi log Serilog trong request có UserId (đặt sau UseAuthentication).</summary>
    public static IApplicationBuilder UseUserLogContext(this IApplicationBuilder app) =>
        app.Use(async (context, nextMiddleware) =>
        {
            var userId = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            using (LogContext.PushProperty("UserId", userId ?? "anonymous"))
            {
                await nextMiddleware(context);
            }
        });
}
