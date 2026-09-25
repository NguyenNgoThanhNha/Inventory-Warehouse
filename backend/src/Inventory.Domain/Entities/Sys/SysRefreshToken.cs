using Inventory.Domain.Common;

namespace Inventory.Domain.Entities.Sys;

/// <summary>Refresh token — chỉ lưu hash SHA-256.</summary>
public class SysRefreshToken : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public required string TokenHash { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByTokenHash { get; set; }

    public SysAccount User { get; set; } = null!;

    public bool IsActive(DateTime now) => RevokedAt is null && ExpiresAt > now;

    public void Revoke(DateTime now, string? replacedByTokenHash = null)
    {
        RevokedAt ??= now;
        ReplacedByTokenHash ??= replacedByTokenHash;
    }
}

/// <summary>Thông báo in-app.</summary>
public class SysNotification : BaseEntity
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public required string Message { get; set; }
    /// <summary>Đường dẫn FE mở khi bấm thông báo (vd: /stock?belowThreshold=true).</summary>
    public string? Link { get; set; }
    public bool IsRead { get; set; }
}
