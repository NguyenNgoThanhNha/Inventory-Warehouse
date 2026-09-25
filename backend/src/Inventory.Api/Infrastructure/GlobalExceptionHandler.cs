using System.Diagnostics;
using Inventory.Application.Common.Behaviors;
using Inventory.Application.Common.Exceptions;
using Inventory.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Api.Infrastructure;

/// <summary>Ánh xạ exception → ProblemDetails (chuẩn BE §8). traceId trùng với Sys_LogApi.TraceId để tra log.</summary>
public sealed class GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "Dữ liệu không hợp lệ"),
            BadHttpRequestException => (StatusCodes.Status400BadRequest, "Request không hợp lệ"),
            UnauthorizedException => (StatusCodes.Status401Unauthorized, "Chưa xác thực"),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Không đủ quyền"),
            NotFoundException or KeyNotFoundException or FileNotFoundException => (StatusCodes.Status404NotFound, "Không tìm thấy"),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Xung đột dữ liệu"),
            DbUpdateException db when ConflictRetryBehavior<object, object>.IsUniqueViolation(db) => (StatusCodes.Status409Conflict, "Xung đột dữ liệu"),
            InsufficientStockException => (StatusCodes.Status409Conflict, "Không đủ hàng"),
            ConflictException or DomainException => (StatusCodes.Status409Conflict, "Xung đột nghiệp vụ"),
            _ => (StatusCodes.Status500InternalServerError, "Lỗi hệ thống")
        };

        if (status >= 500) logger.LogError(exception, "Unhandled exception");
        else logger.LogInformation("Request failed with {StatusCode}: {Message}", status, exception.Message);

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = exception switch
            {
                _ when status >= 500 => "Đã có lỗi xảy ra. Vui lòng thử lại hoặc liên hệ quản trị viên kèm traceId.",
                DbUpdateConcurrencyException or DbUpdateException => Domain.Constants.ConstMessage.ConcurrencyConflict,
                _ => exception.Message
            },
            Instance = context.Request.Path
        };
        problem.Extensions["traceId"] = TraceIdOf(context);
        if (exception is ValidationException validation) problem.Extensions["errors"] = validation.Errors;
        if (exception is InsufficientStockException stock) problem.Extensions["shortages"] = stock.Shortages; // FE tô đỏ đúng dòng thiếu

        context.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context, ProblemDetails = problem, Exception = exception
        });
    }

    public static string TraceIdOf(HttpContext context) => Activity.Current?.Id ?? context.TraceIdentifier;
}
