using Inventory.Domain.Enums;

namespace Inventory.Application.Common.Security;

public readonly record struct PermissionFlags(bool C, bool R, bool U, bool D)
{
    public static readonly PermissionFlags All = new(true, true, true, true);

    public bool Allows(ActivityType type) => type switch
    {
        ActivityType.Create => C,
        ActivityType.Read => R,
        ActivityType.Update => U,
        ActivityType.Delete => D,
        _ => false
    };

    public bool Any => C || R || U || D;

    public PermissionFlags Or(PermissionFlags other) =>
        new(C || other.C, R || other.R, U || other.U, D || other.D);
}

/// <summary>Quyền hiệu lực của một user (kết quả gộp 6 bảng).</summary>
public sealed record EffectivePermissions(
    Guid UserId,
    bool IsActive,
    bool IsAdmin,
    IReadOnlyList<string> Roles,
    IReadOnlyDictionary<string, PermissionFlags> Activities)
{
    public static EffectivePermissions None(Guid userId) =>
        new(userId, false, false, [], new Dictionary<string, PermissionFlags>());

    public bool Has(string activityCode, ActivityType type) =>
        IsActive && (IsAdmin || (Activities.TryGetValue(activityCode, out var flags) && flags.Allows(type)));
}

/// <summary>Chuỗi quyền dạng "TICKET:C" (dùng cho tên policy và thông báo lỗi).</summary>
public static class PermissionKey
{
    public static string Format(string activityCode, ActivityType type) => $"{activityCode}:{ToChar(type)}";

    public static char ToChar(ActivityType type) => type switch
    {
        ActivityType.Create => 'C',
        ActivityType.Read => 'R',
        ActivityType.Update => 'U',
        ActivityType.Delete => 'D',
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    public static bool TryParse(string key, out string activityCode, out ActivityType type)
    {
        activityCode = string.Empty;
        type = default;
        var parts = key.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length != 1) return false;

        activityCode = parts[0].ToUpperInvariant();
        switch (char.ToUpperInvariant(parts[1][0]))
        {
            case 'C': type = ActivityType.Create; return true;
            case 'R': type = ActivityType.Read; return true;
            case 'U': type = ActivityType.Update; return true;
            case 'D': type = ActivityType.Delete; return true;
            default: return false;
        }
    }
}
