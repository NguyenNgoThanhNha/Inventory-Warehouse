namespace Inventory.Application.Common.Models;

/// <summary>Cấu hình chung của ứng dụng (section "App").</summary>
public sealed class AppOptions
{
    public const string SectionName = "App";

    /// <summary>URL gốc của FE — dùng dựng link trong email (reset mật khẩu...).</summary>
    public string FrontendBaseUrl { get; set; } = "http://localhost:5173";
}
