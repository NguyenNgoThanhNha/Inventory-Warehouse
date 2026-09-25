using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ClosedXML.Excel;

namespace Inventory.IntegrationTests;

/// <summary>Import/Export Excel + cảnh báo tồn thấp qua HTTP trên SQL Server thật (DoD: import 1.000+ dòng không timeout).</summary>
[Collection(ApiCollection.Name)]
public class ImportExportTests(InventoryApiFactory factory)
{
    private const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static byte[] Workbook(string[] headers, IEnumerable<object?[]> rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Data");
        for (var c = 0; c < headers.Length; c++) ws.Cell(1, c + 1).Value = headers[c];
        var r = 2;
        foreach (var row in rows)
        {
            for (var c = 0; c < row.Length; c++)
                ws.Cell(r, c + 1).Value = row[c] switch
                {
                    null => Blank.Value,
                    string s => s,
                    int i => i,
                    decimal d => d,
                    var o => o.ToString()
                };
            r++;
        }
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static MultipartFormDataContent Upload(byte[] content, string fileName = "data.xlsx")
    {
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(Xlsx);
        return new MultipartFormDataContent { { file, "file", fileName } };
    }

    [Fact]
    public async Task Imports_1500_products_in_batches_reporting_bad_rows_then_is_idempotent()
    {
        var admin = await factory.CreateClientForAsync("admin@inventory.local", "Admin@123");
        var prefix = $"IMP{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var group = $"Nhóm {prefix}";
        var rows = Enumerable.Range(1, 1500).Select(i => new object?[]
        {
            $"{prefix}-{i:D5}", $"Hàng import {i}", "cái", group, 1000m + i, 1500m + i, null
        }).ToList();
        rows[99] = [$"{prefix}-00100", "", "cái", group, 1m, 1m, null];      // dòng 101: thiếu tên
        rows[199] = [$"{prefix}-00200", "Âm", "cái", group, -1m, 1m, null];   // dòng 201: giá âm
        var file = Workbook(["SKU", "Tên sản phẩm", "Đơn vị", "Nhóm hàng", "Giá vốn", "Giá bán", "Đang kinh doanh"], rows);

        var dry = await (await admin.PostAsync("/api/v1/imports/products?dryRun=true", Upload(file))).Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal(1498, dry!["created"]!.GetValue<int>());
        Assert.Equal(0, (await admin.GetFromJsonAsync<JsonObject>($"/api/v1/products?search={prefix}"))!["totalCount"]!.GetValue<int>());

        var sw = Stopwatch.StartNew();
        var response = await admin.PostAsync("/api/v1/imports/products", Upload(file));
        sw.Stop();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal((1500, 1498, 1498, 0), (result["totalRows"]!.GetValue<int>(), result["validRows"]!.GetValue<int>(),
            result["created"]!.GetValue<int>(), result["updated"]!.GetValue<int>()));
        Assert.Equal([101, 201], result["errors"]!.AsArray().Select(e => e!["row"]!.GetValue<int>()));
        Assert.Equal([group], result["groupsCreated"]!.AsArray().Select(g => g!.GetValue<string>()));
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(30), $"Import took {sw.Elapsed}");
        Assert.Equal(1498, (await admin.GetFromJsonAsync<JsonObject>($"/api/v1/products?search={prefix}"))!["totalCount"]!.GetValue<int>());

        // Chạy lại cùng file (vd: đứt mạng rồi gửi lại) → chỉ cập nhật, không nhân đôi.
        var again = (await (await admin.PostAsync("/api/v1/imports/products", Upload(file))).Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal((0, 1498), (again["created"]!.GetValue<int>(), again["updated"]!.GetValue<int>()));
    }

    [Fact]
    public async Task Import_rejects_wrong_files_and_requires_permission()
    {
        var admin = await factory.CreateClientForAsync("admin@inventory.local", "Admin@123");
        var notXlsx = await admin.PostAsync("/api/v1/imports/products", Upload("hello"u8.ToArray(), "data.csv"));
        Assert.Equal(HttpStatusCode.BadRequest, notXlsx.StatusCode);
        var wrongColumns = await admin.PostAsync("/api/v1/imports/products", Upload(Workbook(["A", "B"], [["1", "2"]])));
        Assert.Equal(HttpStatusCode.BadRequest, wrongColumns.StatusCode);
        Assert.Contains("Thiếu cột", await wrongColumns.Content.ReadAsStringAsync());

        var manager = await factory.CreateClientForAsync("manager@inventory.local", "Manager@123");
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsync("/api/v1/imports/products", Upload(Workbook(["SKU"], [])))).StatusCode);
    }

    [Fact]
    public async Task Parses_document_lines_for_the_form_without_writing()
    {
        var staff = await factory.CreateClientForAsync("staff@inventory.local", "Staff@123");
        var file = Workbook(["SKU", "Số lượng", "Ghi chú"], [["p001", "1.500", "giao gấp"], ["NOPE", 1, null], ["P001", 2, null]]);

        var response = await staff.PostAsync("/api/v1/imports/document-lines?type=GoodsIssue", Upload(file));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var parsed = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        var line = parsed["lines"]!.AsArray().Single()!;
        Assert.Equal(("P001", 1500m), (line["sku"]!.GetValue<string>(), line["quantity"]!.GetValue<decimal>()));
        Assert.Equal([3, 4], parsed["errors"]!.AsArray().Select(e => e!["row"]!.GetValue<int>()));
    }

    [Fact]
    public async Task Exports_stock_and_kardex_as_real_xlsx_and_serves_templates()
    {
        var manager = await factory.CreateClientForAsync("manager@inventory.local", "Manager@123");

        var stock = await manager.GetAsync("/api/v1/exports/stock?warehouseId=1");
        Assert.Equal(HttpStatusCode.OK, stock.StatusCode);
        Assert.Equal(Xlsx, stock.Content.Headers.ContentType?.MediaType);
        using (var wb = new XLWorkbook(await stock.Content.ReadAsStreamAsync()))
        {
            var ws = wb.Worksheet(1);
            Assert.Equal("SKU", ws.Cell(3, 1).GetString()); // dòng 1 ghi chú, dòng 2 trống, dòng 3 tiêu đề
            Assert.Equal("KHO-A", ws.Cell(3, 5).GetString());
            Assert.True(ws.RowsUsed().Count() > 3);
        }

        var kardex = await manager.GetAsync("/api/v1/exports/kardex?productId=1&warehouseId=1");
        Assert.Equal(HttpStatusCode.OK, kardex.StatusCode);
        using (var wb = new XLWorkbook(await kardex.Content.ReadAsStreamAsync()))
            Assert.Contains("Tồn đầu kỳ", wb.Worksheet(1).Column(3).CellsUsed().Select(c => c.GetString()));

        var template = await manager.GetAsync("/api/v1/imports/templates/Products");
        Assert.Equal(HttpStatusCode.OK, template.StatusCode);
        Assert.Contains("mau-import-san-pham.xlsx", template.Content.Headers.ContentDisposition?.ToString());

        var staff = await factory.CreateClientForAsync("staff@inventory.local", "Staff@123"); // không có IMPORT_EXPORT:R
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/v1/exports/stock")).StatusCode);
    }

    [Fact]
    public async Task Scan_opens_alerts_for_low_stock_and_notifies_managers()
    {
        var manager = await factory.CreateClientForAsync("manager@inventory.local", "Manager@123");
        var admin = await factory.CreateClientForAsync("admin@inventory.local", "Admin@123");
        var groups = await admin.GetFromJsonAsync<JsonArray>("/api/v1/product-groups");
        var product = await (await admin.PostAsJsonAsync("/api/v1/products", new
        {
            sku = $"ALR-{Guid.NewGuid():N}"[..20], name = "Alert item", unit = "cái", groupId = groups![0]!["id"]!.GetValue<int>(), cost = 1, price = 1
        })).Content.ReadFromJsonAsync<JsonObject>();
        var productId = product!["id"]!.GetValue<int>();
        (await manager.PutAsJsonAsync("/api/v1/stock/threshold", new { productId, warehouseId = 1, minThreshold = 10 })).EnsureSuccessStatusCode();

        var scan = (await (await manager.PostAsync("/api/v1/stock-alerts/scan", null)).Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.True(scan["opened"]!.GetValue<int>() >= 1);

        var alerts = (await manager.GetFromJsonAsync<JsonObject>("/api/v1/stock-alerts?pageSize=100"))!;
        Assert.Contains(alerts["items"]!.AsArray(), a => a!["productId"]!.GetValue<int>() == productId);
        var notes = await manager.GetFromJsonAsync<JsonArray>("/api/v1/notifications");
        Assert.Contains(notes!, n => n!["link"]!.GetValue<string>() == "/stock?belowThreshold=true&warehouseId=1");

        var staff = await factory.CreateClientForAsync("staff@inventory.local", "Staff@123");
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsync("/api/v1/stock-alerts/scan", null)).StatusCode);
    }
}
