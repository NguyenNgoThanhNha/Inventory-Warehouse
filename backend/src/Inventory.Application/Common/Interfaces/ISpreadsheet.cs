using System.Globalization;
using System.Text;

namespace Inventory.Application.Common.Interfaces;

/// <summary>
/// Một dòng dữ liệu đọc từ sheet: số dòng Excel (để báo lỗi) + giá trị GỐC theo tên cột đã chuẩn hóa:
/// <c>double</c> (ô số), <c>bool</c>, <c>DateTime</c> hoặc <c>string</c>. Giữ kiểu gốc để ô số 1.234 không bị
/// hiểu nhầm thành "1.234" (một nghìn hai trăm ba mươi tư) khi đọc như chữ kiểu Việt Nam.
/// </summary>
public sealed record SheetRow(int RowNumber, IReadOnlyDictionary<string, object?> Cells)
{
    /// <summary>Giá trị ô dạng chữ (số → chuỗi invariant); rỗng → null.</summary>
    public string? Get(string header) => GetRaw(header) switch
    {
        null => null,
        string s => string.IsNullOrWhiteSpace(s) ? null : s.Trim(),
        double d => d.ToString(CultureInfo.InvariantCulture),
        DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        var other => Convert.ToString(other, CultureInfo.InvariantCulture)
    };

    /// <summary>Giá trị gốc theo tiêu đề cột (so khớp không phân biệt hoa thường / dấu tiếng Việt).</summary>
    public object? GetRaw(string header) => Cells.TryGetValue(SheetHeader.Normalize(header), out var v) ? v : null;

    public bool TryGetDecimal(string header, out decimal value) => SheetValue.TryDecimal(GetRaw(header), out value);
}

public sealed record SheetReadResult(IReadOnlyList<string> Headers, IReadOnlyList<SheetRow> Rows);

public enum SheetColumnFormat
{
    Text,
    Integer,
    Quantity,
    Money,
    DateTime
}

public sealed record SheetColumn(string Header, SheetColumnFormat Format = SheetColumnFormat.Text, double Width = 16);

/// <summary>Một sheet để ghi: <see cref="Rows"/> là mảng giá trị theo đúng thứ tự <see cref="Columns"/>.</summary>
public sealed record SheetData(string Name, IReadOnlyList<SheetColumn> Columns, IEnumerable<object?[]> Rows, IReadOnlyList<string>? Notes = null);

/// <summary>File trả về cho client tải.</summary>
public sealed record FileDto(byte[] Content, string ContentType, string FileName)
{
    public const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
}

/// <summary>Đọc/ghi .xlsx — Application không phụ thuộc thư viện Excel cụ thể (Infrastructure dùng ClosedXML).</summary>
public interface ISpreadsheetService
{
    /// <summary>
    /// Đọc sheet đầu tiên: dòng 1 là tiêu đề, bỏ qua dòng trống. File hỏng / không phải xlsx / quá
    /// <paramref name="maxRows"/> dòng → <c>ValidationException("file", ...)</c>.
    /// </summary>
    SheetReadResult Read(Stream content, int maxRows);

    byte[] Write(IReadOnlyList<SheetData> sheets);
}

public static class SheetHeader
{
    /// <summary>"Tên sản phẩm" / "ten san pham" / " TÊN  SẢN PHẨM " → "ten san pham".</summary>
    public static string Normalize(string header)
    {
        var decomposed = header.Trim().ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        var lastSpace = false;
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            var space = char.IsWhiteSpace(c);
            if (space && lastSpace) continue;
            sb.Append(space ? ' ' : c);
            lastSpace = space;
        }
        return sb.ToString();
    }
}

/// <summary>Đọc số từ ô Excel.</summary>
public static partial class SheetValue
{
    [System.Text.RegularExpressions.GeneratedRegex(@"^-?\d{1,3}(\.\d{3})+(,\d+)?$")]
    private static partial System.Text.RegularExpressions.Regex ViGrouped();

    /// <summary>
    /// Ô số → lấy thẳng. Ô chữ (người dùng gõ tay): theo thói quen Việt Nam — "35.000" / "1.234.567" là phân tách
    /// hàng nghìn, dấu phẩy là thập phân ("1,5"); còn lại đọc kiểu invariant ("1234.5").
    /// </summary>
    public static bool TryDecimal(object? raw, out decimal value)
    {
        value = 0;
        switch (raw)
        {
            case double d when !double.IsNaN(d) && !double.IsInfinity(d) && Math.Abs(d) < 7.9e27:
                value = (decimal)d;
                return true;
            case string text when !string.IsNullOrWhiteSpace(text):
                var s = text.Trim().Replace(" ", "");
                var vi = CultureInfo.GetCultureInfo("vi-VN");
                if (ViGrouped().IsMatch(s) || (s.Contains(',') && !s.Contains('.')))
                    return decimal.TryParse(s, NumberStyles.Number, vi, out value);
                return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out value)
                       || decimal.TryParse(s, NumberStyles.Number, vi, out value);
            default:
                return false;
        }
    }

    /// <summary>Có/không: "x", "1", "true", "có", "yes" là true; "0", "false", "không", "no" là false.</summary>
    public static bool? TryBool(object? raw) => raw is bool b ? b : SheetHeader.Normalize(Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty) switch
    {
        "" => null,
        "x" or "1" or "true" or "co" or "yes" or "y" => true,
        "0" or "false" or "khong" or "no" or "n" => false,
        _ => null
    };
}
