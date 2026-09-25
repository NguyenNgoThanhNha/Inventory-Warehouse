using Inventory.Application.Common.Security;
using Inventory.Domain.Enums;
using Inventory.Persistence.Interceptors;

namespace Inventory.Application.Common.Interfaces;

/// <summary>User của request hiện tại (Api cài đặt từ JWT). Quyền được tra qua IPermissionService (có cache).</summary>
public interface ICurrentUser : IAuditUser
{
    bool IsAuthenticated { get; }

    /// <summary>Ném UnauthorizedException nếu chưa đăng nhập.</summary>
    Guid UserId { get; }

    Task<EffectivePermissions> GetPermissionsAsync(CancellationToken ct = default);

    async Task<bool> HasPermissionAsync(string activityCode, ActivityType type, CancellationToken ct = default) =>
        (await GetPermissionsAsync(ct)).Has(activityCode, type);
}

public interface IPermissionService
{
    /// <summary>Quyền hiệu lực = Admin → toàn quyền; ngược lại OR(quyền các role, quyền riêng). Có cache.</summary>
    Task<EffectivePermissions> GetEffectiveAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Gọi khi đổi role / quyền riêng của một user.</summary>
    void Invalidate(Guid userId);

    /// <summary>Gọi khi sửa quyền của role (ảnh hưởng nhiều user).</summary>
    void InvalidateAll();
}
