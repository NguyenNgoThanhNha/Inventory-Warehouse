using System.IdentityModel.Tokens.Jwt;
using Inventory.Application.Common.Exceptions;
using Inventory.Application.Common.Interfaces;
using Inventory.Application.Common.Security;

namespace Inventory.Api.Infrastructure;

/// <summary>
/// Người thực hiện cho AuditSaveChangesInterceptor — CHỈ đọc claim, không phụ thuộc DbContext.
/// (Không dùng CurrentUser cho IAuditUser: CurrentUser → IPermissionService → DbContext → interceptor → vòng lặp DI.)
/// </summary>
public sealed class HttpAuditUser(IHttpContextAccessor accessor) : Persistence.Interceptors.IAuditUser
{
    public Guid? UserIdOrNull =>
        Guid.TryParse(accessor.HttpContext?.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) ? id : null;

    public string? UserName => accessor.HttpContext?.User.FindFirst(JwtRegisteredClaimNames.Name)?.Value;
}

/// <summary>User của request hiện tại, đọc từ JWT (sub, name). Quyền lấy qua IPermissionService, nhớ trong phạm vi request.</summary>
public sealed class CurrentUser(IHttpContextAccessor accessor, IPermissionService permissionService) : ICurrentUser
{
    private EffectivePermissions? _permissions;

    public bool IsAuthenticated => accessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public Guid? UserIdOrNull =>
        Guid.TryParse(accessor.HttpContext?.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) ? id : null;

    public string? UserName => accessor.HttpContext?.User.FindFirst(JwtRegisteredClaimNames.Name)?.Value;

    public Guid UserId => UserIdOrNull ?? throw new UnauthorizedException();

    public async Task<EffectivePermissions> GetPermissionsAsync(CancellationToken ct = default)
    {
        if (UserIdOrNull is not { } userId) return EffectivePermissions.None(Guid.Empty);
        return _permissions ??= await permissionService.GetEffectiveAsync(userId, ct);
    }
}
