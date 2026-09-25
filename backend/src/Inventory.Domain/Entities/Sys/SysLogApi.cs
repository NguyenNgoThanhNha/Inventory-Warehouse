namespace Inventory.Domain.Entities.Sys;

/// <summary>
/// Log request/response API để debug (chuẩn BE §9.2). Chỉ thêm — không sửa, không xóa mềm
/// nên không kế thừa BaseEntity; job dọn dẹp xóa cứng theo RetentionDays.
/// </summary>
public class SysLogApi
{
    public long Id { get; set; }
    public required string Module { get; set; }
    public required string TraceId { get; set; }
    public string? Ip { get; set; }
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public required string Method { get; set; }
    public required string Url { get; set; }
    public int StatusCode { get; set; }
    public long DurationMs { get; set; }
    public string? Request { get; set; }
    public string? Response { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedDate { get; set; }
}
