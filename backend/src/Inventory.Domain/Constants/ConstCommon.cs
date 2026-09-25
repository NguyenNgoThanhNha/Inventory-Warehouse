namespace Inventory.Domain.Constants;

public static class ConstMessage
{
    public const string NotFound = "{0} '{1}' không tồn tại.";
    public const string Forbidden = "Bạn không có quyền thực hiện thao tác này.";
    public const string Unauthorized = "Chưa xác thực hoặc thông tin đăng nhập không hợp lệ.";
    public const string InvalidCredentials = "Email hoặc mật khẩu không đúng.";
    public const string EmailExists = "Email đã được sử dụng.";
    public const string ConcurrencyConflict = "Dữ liệu đã được người khác cập nhật. Vui lòng tải lại.";
    public const string InsufficientStock = "Không đủ hàng: {0}.";
    public const string DocumentNotDraft = "Phiếu {0} không còn ở trạng thái nháp.";
}

public static class ConstCacheKey
{
    public static string UserPermission(Guid userId, long version) => $"perm:{version}:{userId:N}";

    /// <summary>Token phiên bản của cache danh mục (đổi token = vô hiệu toàn bộ).</summary>
    public const string CatalogVersion = "catalog:version";

    public static string Catalog(string version, string name) => $"catalog:{version}:{name}";

    public const string ProductGroups = "product-groups";

    public static string Warehouses(bool includeInactive) => $"warehouses:{(includeInactive ? "all" : "active")}";

    public static string ProductSearch(string? search, int? groupId, bool? isActive, int page, int pageSize) =>
        $"products:{search?.Trim().ToLowerInvariant()}|{groupId}|{isActive}|{page}|{pageSize}";
}

/// <summary>Tiền tố mã chứng từ: PN-2026-00001, PX-..., CK-..., KK-...</summary>
public static class ConstDocumentPrefix
{
    public const string GoodsReceipt = "PN";
    public const string GoodsIssue = "PX";
    public const string Transfer = "CK";
    public const string StockTake = "KK";
}

public static class ConstHeader
{
    /// <summary>Header chống double-submit khi tạo phiếu.</summary>
    public const string IdempotencyKey = "Idempotency-Key";
}

public static class ConstPolicy
{
    /// <summary>Tiền tố tên policy động: "PERM:GOODS_ISSUE:C" hoặc "PERM:USER:R|ROLE:R" (OR).</summary>
    public const string PermissionPrefix = "PERM:";
}

public static class ConstTable
{
    public const string Account = "Sys_Account";
    public const string Role = "Sys_Role";
    public const string Activity = "Sys_Activity";
    public const string UserRole = "Sys_UserRole";
    public const string RoleActivity = "Sys_RoleActivity";
    public const string UserActivity = "Sys_UserActivity";
    public const string RefreshToken = "Sys_RefreshToken";
    public const string Notification = "Sys_Notification";
    public const string LogApi = "Sys_LogApi";
}
