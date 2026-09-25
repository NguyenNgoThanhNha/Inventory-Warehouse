namespace Inventory.Domain.Constants;

/// <summary>
/// Mã Activity (chức năng) cho phân quyền 6 bảng. Seeder đồng bộ danh sách <see cref="All"/> vào Sys_Activity.
/// Thêm chức năng mới = thêm hằng số + một dòng trong <see cref="All"/>.
/// </summary>
public static class ConstActivity
{
    public const string ApplicationName = "Inventory";

    public const string Product = "PRODUCT";
    public const string Warehouse = "WAREHOUSE";
    public const string Supplier = "SUPPLIER";
    public const string GoodsReceipt = "GOODS_RECEIPT";
    public const string GoodsIssue = "GOODS_ISSUE";
    public const string Transfer = "TRANSFER";
    public const string StockTake = "STOCK_TAKE";
    public const string StockReport = "STOCK_REPORT";
    public const string ImportExport = "IMPORT_EXPORT";
    public const string User = "USER";
    public const string Role = "ROLE";
    public const string ApiLog = "API_LOG";

    public sealed record Definition(string Code, string Name, string Description);

    public static readonly IReadOnlyList<Definition> All =
    [
        new(Product, "Sản phẩm", "C: thêm · R: xem · U: sửa sản phẩm, nhóm hàng · D: xóa"),
        new(Warehouse, "Kho", "C: thêm · R: xem · U: sửa kho, đặt ngưỡng tồn tối thiểu · D: xóa"),
        new(Supplier, "Nhà cung cấp", "C: thêm · R: xem · U: sửa · D: xóa"),
        new(GoodsReceipt, "Phiếu nhập kho", "C: lập phiếu nháp · R: xem · U: duyệt/ghi sổ · D: hủy phiếu nháp"),
        new(GoodsIssue, "Phiếu xuất kho", "C: lập phiếu nháp · R: xem · U: duyệt/ghi sổ · D: hủy phiếu nháp"),
        new(Transfer, "Chuyển kho", "C: lập phiếu nháp · R: xem · U: duyệt/ghi sổ · D: hủy phiếu nháp"),
        new(StockTake, "Kiểm kê", "C: lập phiếu nháp · R: xem · U: duyệt/ghi sổ điều chỉnh · D: hủy phiếu nháp"),
        new(StockReport, "Tồn kho & báo cáo", "R: xem tồn kho, thẻ kho, báo cáo"),
        new(ImportExport, "Import/Export", "C: import Excel · R: export Excel"),
        new(User, "Người dùng", "R: xem user · U: khóa/mở, gán role, cấp quyền riêng"),
        new(Role, "Vai trò", "C: tạo · R: xem · U: sửa quyền role · D: xóa role"),
        new(ApiLog, "Log API", "R: xem log request/response API để debug")
    ];
}
