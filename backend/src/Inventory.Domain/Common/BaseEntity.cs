namespace Inventory.Domain.Common;

/// <summary>
/// Field audit + xóa mềm dùng chung (giữ nguyên field của Backend_Api_Template).
/// Được AuditSaveChangesInterceptor tự điền — handler không gán tay.
/// </summary>
public abstract class BaseEntity
{
    public DateTime CreatedDate { get; set; }
    public Guid? CreatedById { get; set; }
    public string? CreatedName { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public Guid? UpdatedById { get; set; }
    public string? Updater { get; set; }
    public bool IsDeleted { get; set; }
}

/// <summary>
/// Entity tự quyết định người tạo (null nghĩa là "Hệ thống", vd: cảnh báo do job sinh ra).
/// AuditSaveChangesInterceptor sẽ KHÔNG tự điền CreatedById/CreatedName cho entity này.
/// </summary>
public interface IExplicitCreator;
