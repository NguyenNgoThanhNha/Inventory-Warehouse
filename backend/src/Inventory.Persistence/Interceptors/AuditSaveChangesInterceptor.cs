using Inventory.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Inventory.Persistence.Interceptors;

/// <summary>Người thực hiện thao tác — Application cài đặt qua ICurrentUser.</summary>
public interface IAuditUser
{
    Guid? UserIdOrNull { get; }
    string? UserName { get; }
}

/// <summary>
/// Tự gán Created*/Updated* cho mọi BaseEntity và đổi Remove() thành xóa mềm (IsDeleted = true).
/// Giá trị đã gán sẵn trong domain (vd: CreatedDate gán sẵn khi import) được giữ nguyên.
/// </summary>
public sealed class AuditSaveChangesInterceptor(IAuditUser auditUser, TimeProvider clock) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null) return;
        var now = clock.GetUtcNow().UtcDateTime;
        var userId = auditUser.UserIdOrNull;
        var userName = auditUser.UserName;

        foreach (var entry in context.ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    if (entry.Entity.CreatedDate == default) entry.Entity.CreatedDate = now;
                    if (entry.Entity is IExplicitCreator) break;
                    entry.Entity.CreatedById ??= userId;
                    entry.Entity.CreatedName ??= userName;
                    break;

                case EntityState.Deleted:
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    SetUpdated(entry.Entity, now, userId, userName);
                    break;

                case EntityState.Modified:
                    SetUpdated(entry.Entity, now, userId, userName);
                    break;
            }
        }
    }

    private static void SetUpdated(BaseEntity entity, DateTime now, Guid? userId, string? userName)
    {
        entity.UpdatedDate = now;
        entity.UpdatedById = userId;
        entity.Updater = userName;
    }
}
