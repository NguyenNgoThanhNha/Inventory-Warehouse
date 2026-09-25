using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Inventory.Application.Common.Behaviors;

/// <summary>
/// Đánh dấu command được chạy lại khi lưu bị xung đột (optimistic concurrency / unique index).
/// Chỉ gắn cho command mà chạy lại từ đầu là an toàn: handler tự đọc lại dữ liệu mới nhất và kiểm tra lại mọi rule.
/// </summary>
public interface IRetryOnConflict;

/// <summary>
/// Tự thử lại command <see cref="IRetryOnConflict"/> khi SaveChanges gặp:
/// - <see cref="DbUpdateConcurrencyException"/>: RowVersion đã đổi (vd: phiếu khác vừa trừ cùng dòng tồn kho).
/// - Vi phạm unique index (SQL 2601/2627): hai request cùng tạo một dòng (StockLevel mới, số chứng từ, Idempotency-Key).
/// Mỗi lần thử lại xóa change tracker rồi chạy lại handler: tồn kho được đọc lại và kiểm tra lại, nên
/// phiếu đến sau hoặc thành công với tồn mới, hoặc bị chặn bằng InsufficientStockException (409) có thông báo rõ.
/// Hết số lần thử thì ném tiếp để GlobalExceptionHandler trả 409.
/// </summary>
public sealed class ConflictRetryBehavior<TRequest, TResponse>(
    IUnitOfWork<InventoryDbContext> unitOfWork,
    ILogger<ConflictRetryBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public const int MaxAttempts = 4;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not IRetryOnConflict) return await next(cancellationToken);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await next(cancellationToken);
            }
            catch (DbUpdateException ex) when (attempt < MaxAttempts && IsRetryable(ex))
            {
                logger.LogWarning("Save conflict on {RequestName} (attempt {Attempt}/{MaxAttempts}): {Reason}",
                    typeof(TRequest).Name, attempt, MaxAttempts, ex is DbUpdateConcurrencyException ? "rowversion" : "unique index");
                unitOfWork.ClearChangeTracker();
                await Task.Delay(Random.Shared.Next(10, 40) * attempt, cancellationToken); // giãn nhịp để các request không va lại cùng lúc
            }
        }
    }

    public static bool IsRetryable(DbUpdateException ex) =>
        ex is DbUpdateConcurrencyException || IsUniqueViolation(ex);

    public static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };
}
