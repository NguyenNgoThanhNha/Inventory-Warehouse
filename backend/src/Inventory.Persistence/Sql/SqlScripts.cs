namespace Inventory.Persistence.Sql;

/// <summary>
/// Đọc script SQL (stored procedure) nhúng trong assembly để migration chạy (chuẩn BE §3.3).
/// Sửa SP: sửa file .sql (luôn dùng CREATE OR ALTER) rồi tạo migration mới gọi lại <see cref="Read"/>.
/// </summary>
public static class SqlScripts
{
    public static string Read(string fileName)
    {
        var assembly = typeof(SqlScripts).Assembly;
        var resource = assembly.GetManifestResourceNames().SingleOrDefault(n => n.EndsWith("." + fileName, StringComparison.Ordinal))
                       ?? throw new InvalidOperationException($"Không tìm thấy script SQL nhúng '{fileName}'.");
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
