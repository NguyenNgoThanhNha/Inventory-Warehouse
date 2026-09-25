using Inventory.Domain.Common;
using Inventory.Domain.Constants;
using Inventory.Domain.Enums;

namespace Inventory.Domain.Entities.Sys;

/// <summary>Sys_Account — tài khoản người dùng.</summary>
public class SysAccount : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Email { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public required string FullName { get; set; }
    public bool IsActive { get; set; } = true;
    public string? PasswordResetTokenHash { get; set; }
    public DateTime? PasswordResetTokenExpiresAt { get; set; }

    public ICollection<SysUserRole> UserRoles { get; set; } = new List<SysUserRole>();
    public ICollection<SysUserActivity> UserActivities { get; set; } = new List<SysUserActivity>();

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public void SetPasswordHash(string hash)
    {
        PasswordHash = hash;
        PasswordResetTokenHash = null;
        PasswordResetTokenExpiresAt = null;
    }

    public bool IsPasswordResetTokenValid(string tokenHash, DateTime now) =>
        PasswordResetTokenHash is not null
        && PasswordResetTokenExpiresAt > now
        && string.Equals(PasswordResetTokenHash, tokenHash, StringComparison.Ordinal);
}

/// <summary>Sys_Role — RoleType = 1 là Admin (toàn quyền).</summary>
public class SysRole : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public string? Description { get; set; }
    public int? RoleType { get; set; }

    public bool IsAdmin => RoleType == ConstRole.AdminRoleType;

    public ICollection<SysUserRole> UserRoles { get; set; } = new List<SysUserRole>();
    public ICollection<SysRoleActivity> RoleActivities { get; set; } = new List<SysRoleActivity>();
}

/// <summary>Sys_Activity — một chức năng cần phân quyền (mã trong ConstActivity).</summary>
public class SysActivity : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? ApplicationName { get; set; }
}

/// <summary>Sys_UserRole — user ↔ role (nhiều-nhiều).</summary>
public class SysUserRole : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public string? Description { get; set; }

    public SysAccount User { get; set; } = null!;
    public SysRole Role { get; set; } = null!;
}

/// <summary>Cờ C/R/U/D dùng chung cho quyền của role và quyền riêng của user.</summary>
public abstract class CrudPermission : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ActivityId { get; set; }
    public bool C { get; set; }
    public bool R { get; set; }
    public bool U { get; set; }
    public bool D { get; set; }
    public string? Description { get; set; }

    public SysActivity Activity { get; set; } = null!;

    public bool IsEmpty => !C && !R && !U && !D;

    public bool Allows(ActivityType type) => type switch
    {
        ActivityType.Create => C,
        ActivityType.Read => R,
        ActivityType.Update => U,
        ActivityType.Delete => D,
        _ => false
    };

    public void SetFlags(bool c, bool r, bool u, bool d)
    {
        C = c;
        R = r;
        U = u;
        D = d;
    }
}

/// <summary>Sys_RoleActivity — quyền của role trên một activity.</summary>
public class SysRoleActivity : CrudPermission
{
    public Guid RoleId { get; set; }
    public SysRole Role { get; set; } = null!;
}

/// <summary>Sys_UserActivity — quyền RIÊNG của một tài khoản (cộng thêm vào quyền từ role).</summary>
public class SysUserActivity : CrudPermission
{
    public Guid UserId { get; set; }
    public SysAccount User { get; set; } = null!;
}
