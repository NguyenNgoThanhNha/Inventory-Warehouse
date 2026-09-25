using System.Globalization;
using ClosedXML.Excel;
using Inventory.Application.Common.Exceptions;
using Inventory.Application.Common.Interfaces;

namespace Inventory.Infrastructure.Services;

public sealed class ClosedXmlSpreadsheetService : ISpreadsheetService
{
    public SheetReadResult Read(Stream content, int maxRows)
    {
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(content);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // File không phải .xlsx hợp lệ — lỗi của người dùng, không phải lỗi hệ thống.
            throw new ValidationException("file", "File không đọc được. Hãy dùng file .xlsx (tải file mẫu để xem định dạng).");
        }

        using (workbook)
        {
            var sheet = workbook.Worksheets.FirstOrDefault()
                        ?? throw new ValidationException("file", "File không có sheet nào.");
            var used = sheet.RangeUsed();
            if (used is null) return new SheetReadResult([], []);

            var headerRow = used.FirstRow();
            var headers = headerRow.Cells().Select(c => (Column: c.Address.ColumnNumber, Name: c.GetString().Trim()))
                .Where(h => h.Name.Length > 0)
                .ToList();

            var dataRows = used.RowsUsed().Skip(1).ToList();
            if (dataRows.Count > maxRows)
                throw new ValidationException("file", $"File có {dataRows.Count} dòng, tối đa {maxRows} dòng mỗi lần.");

            var rows = new List<SheetRow>(dataRows.Count);
            foreach (var row in dataRows)
            {
                var cells = new Dictionary<string, object?>(headers.Count);
                foreach (var (column, name) in headers)
                    cells[SheetHeader.Normalize(name)] = ToValue(row.WorksheetRow().Cell(column));
                if (cells.Values.All(v => v is null || v is string s && string.IsNullOrWhiteSpace(s))) continue;
                rows.Add(new SheetRow(row.RowNumber(), cells));
            }
            return new SheetReadResult(headers.Select(h => h.Name).ToList(), rows);
        }
    }

    /// <summary>Giữ kiểu gốc của ô (double / bool / DateTime / string) — không phụ thuộc định dạng hiển thị.</summary>
    private static object? ToValue(IXLCell cell)
    {
        var value = cell.Value;
        if (value.IsBlank) return null;
        if (value.IsNumber) return value.GetNumber();
        if (value.IsBoolean) return value.GetBoolean();
        if (value.IsDateTime) return value.GetDateTime();
        return cell.GetString();
    }

    public byte[] Write(IReadOnlyList<SheetData> sheets)
    {
        using var workbook = new XLWorkbook();
        foreach (var data in sheets)
        {
            var sheet = workbook.Worksheets.Add(data.Name);
            var headerRow = 1;
            if (data.Notes is { Count: > 0 } notes)
            {
                for (var i = 0; i < notes.Count; i++)
                {
                    sheet.Cell(i + 1, 1).Value = notes[i];
                    sheet.Cell(i + 1, 1).Style.Font.Italic = true;
                }
                headerRow = notes.Count + 2;
            }

            for (var c = 0; c < data.Columns.Count; c++)
            {
                var cell = sheet.Cell(headerRow, c + 1);
                cell.Value = data.Columns[c].Header;
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8EEF7");
                sheet.Column(c + 1).Width = data.Columns[c].Width;
                sheet.Column(c + 1).Style.NumberFormat.Format = data.Columns[c].Format switch
                {
                    SheetColumnFormat.Integer => "#,##0",
                    SheetColumnFormat.Quantity => "#,##0.###",
                    SheetColumnFormat.Money => "#,##0",
                    SheetColumnFormat.DateTime => "dd/mm/yyyy hh:mm",
                    _ => "@"
                };
            }

            var r = headerRow + 1;
            foreach (var values in data.Rows)
            {
                for (var c = 0; c < values.Length && c < data.Columns.Count; c++)
                    sheet.Cell(r, c + 1).Value = ToCellValue(values[c]);
                r++;
            }
            sheet.SheetView.FreezeRows(headerRow);
            if (r > headerRow + 1) sheet.Range(headerRow, 1, r - 1, data.Columns.Count).SetAutoFilter();
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static XLCellValue ToCellValue(object? value) => value switch
    {
        null => Blank.Value,
        string s => s,
        decimal d => d,
        int i => i,
        long l => l,
        double db => db,
        bool b => b,
        DateTime dt => dt,
        _ => value.ToString() ?? string.Empty
    };
}
