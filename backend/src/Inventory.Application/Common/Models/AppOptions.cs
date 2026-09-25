namespace Inventory.Application.Common.Models;

/// <summary>Cấu hình chung của ứng dụng (section "App").</summary>
public sealed class AppOptions
{
    public const string SectionName = "App";

    /// <summary>URL gốc của FE — dùng dựng link trong email (reset mật khẩu...).</summary>
    public string FrontendBaseUrl { get; set; } = "http://localhost:5173";

    /// <summary>
    /// Múi giờ nghiệp vụ (IANA). DB lưu UTC; báo cáo "theo ngày" (hôm nay, nhập–xuất theo ngày, kardex từ ngày…đến ngày)
    /// cắt ngày theo múi giờ này.
    /// </summary>
    public string TimeZoneId { get; set; } = "Asia/Ho_Chi_Minh";

    public TimeZoneInfo TimeZone => TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
}

/// <summary>Đổi ngày địa phương (múi giờ nghiệp vụ) ↔ UTC cho các báo cáo theo ngày.</summary>
public static class BusinessDate
{
    /// <summary>00:00 giờ địa phương của <paramref name="day"/>, đổi sang UTC.</summary>
    public static DateTime StartUtc(DateOnly day, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), zone);

    public static DateOnly Today(DateTime utcNow, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcNow, zone));
}
