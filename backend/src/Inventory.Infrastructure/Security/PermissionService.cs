using Inventory.Application.Common.Interfaces;
using Inventory.Application.Common.Security;
using Inventory.Domain.Constants;
using Inventory.Domain.Entities.Sys;
using Inventory.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Inventory.Infrastructure.Security;

/// <summary>Version của cache quyền — tăng lên để vô hiệu toàn bộ cache (khi sửa quyền role).</summary>
public sealed class PermissionCacheVersion
{
    private long _value;

    public long Value => Interlocked.Read(ref _value);

    public void Increment() => Interlocked.Increment(ref _value);
}

/// <summary>
/// Tính quyền hiệu lực từ 6 bảng (giữ logic GetPermissionOfUser của Backend_Api_Template):
/// role Admin (RoleType = 1) → toàn quyền; ngược lại C/R/U/D = OR(quyền các role, quyền riêng).
/// Kết quả cache IMemoryCache 10 phút; nhiều instance thì thay bằng IDistributedCache (Redis).
/// </summary>
public sealed class PermissionService(
    IUnitOfWork<InventoryDbContext> unitOfWork,
    IMemoryCache cache,
    PermissionCacheVersion version) : IPermissionService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    public async Task<EffectivePermissions> GetEffectiveAsync(Guid userId, CancellationToken ct = default)
    {
        var key = ConstCacheKey.UserPermission(userId, version.Value);
        if (cache.TryGetValue(key, out EffectivePermissions? cached) && cached is not null) return cached;

        var result = await ComputeAsync(userId, ct);
        cache.Set(key, result, CacheDuration);
        return result;
    }

    public void Invalidate(Guid userId) => cache.Remove(ConstCacheKey.UserPermission(userId, version.Value));

    public void InvalidateAll() => version.Increment();

    private async Task<EffectivePermissions> ComputeAsync(Guid userId, CancellationToken ct)
    {
        var account = await unitOfWork.Repository<SysAccount>().AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.IsActive })
            .FirstOrDefaultAsync(ct);
        if (account is null) return EffectivePermissions.None(userId);

        var roles = await unitOfWork.Repository<SysUserRole>().AsNoTracking()
            .Where(ur => ur.UserId == userId)
            .Select(ur => new { ur.Role.Name, ur.Role.RoleType })
            .ToListAsync(ct);
        var isAdmin = roles.Any(r => r.RoleType == ConstRole.AdminRoleType);
        var roleNames = roles.Select(r => r.Name).OrderBy(n => n).ToList();

        var activities = new Dictionary<string, PermissionFlags>(StringComparer.OrdinalIgnoreCase);

        if (isAdmin)
        {
            var codes = await unitOfWork.Repository<SysActivity>().AsNoTracking().Select(a => a.Code).ToListAsync(ct);
            foreach (var code in codes) activities[code] = PermissionFlags.All;
        }
        else
        {
            var fromRoles = await unitOfWork.Repository<SysRoleActivity>().AsNoTracking()
                .Where(ra => ra.Role.UserRoles.Any(ur => ur.UserId == userId))
                .Select(ra => new { ra.Activity.Code, ra.C, ra.R, ra.U, ra.D })
                .ToListAsync(ct);
            var fromUser = await unitOfWork.Repository<SysUserActivity>().AsNoTracking()
                .Where(ua => ua.UserId == userId)
                .Select(ua => new { ua.Activity.Code, ua.C, ua.R, ua.U, ua.D })
                .ToListAsync(ct);

            foreach (var p in fromRoles.Concat(fromUser))
            {
                var flags = new PermissionFlags(p.C, p.R, p.U, p.D);
                activities[p.Code] = activities.TryGetValue(p.Code, out var existing) ? existing.Or(flags) : flags;
            }
        }

        return new EffectivePermissions(userId, account.IsActive, isAdmin, roleNames, activities);
    }
}
