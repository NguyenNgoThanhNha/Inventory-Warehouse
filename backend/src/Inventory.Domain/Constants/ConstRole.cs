namespace Inventory.Domain.Constants;

public static class ConstRole
{
    /// <summary>Giá trị Sys_Role.RoleType của role Admin (toàn quyền, không sửa được quyền).</summary>
    public const int AdminRoleType = 1;

    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Staff = "Staff";

    /// <summary>Role mặc định cho tài khoản tự đăng ký.</summary>
    public const string DefaultForRegistration = Staff;

    /// <summary>Quyền seed mặc định cho role hệ thống (Admin không cần — ngầm định toàn quyền).</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<(string Code, string Flags)>> DefaultPermissions =
        new Dictionary<string, IReadOnlyList<(string, string)>>
        {
            // Quản lý kho: duyệt/ghi sổ phiếu (U), xem báo cáo, đặt ngưỡng cảnh báo (WAREHOUSE:U).
            [Manager] =
            [
                (ConstActivity.Product, "R"),
                (ConstActivity.Warehouse, "RU"),
                (ConstActivity.Supplier, "CRU"),
                (ConstActivity.GoodsReceipt, "CRUD"),
                (ConstActivity.GoodsIssue, "CRUD"),
                (ConstActivity.Transfer, "CRUD"),
                (ConstActivity.StockTake, "CRUD"),
                (ConstActivity.StockReport, "R"),
                (ConstActivity.ImportExport, "R")
            ],
            // Thủ kho: lập phiếu nháp, chờ Manager duyệt.
            [Staff] =
            [
                (ConstActivity.Product, "R"),
                (ConstActivity.Warehouse, "R"),
                (ConstActivity.Supplier, "R"),
                (ConstActivity.GoodsReceipt, "CRD"),
                (ConstActivity.GoodsIssue, "CRD"),
                (ConstActivity.Transfer, "CRD"),
                (ConstActivity.StockTake, "CRD"),
                (ConstActivity.StockReport, "R")
            ]
        };
}
