using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ClosedXML.Excel;

namespace Inventory.IntegrationTests;

/// <summary>Sắp xếp màn Tồn kho (SKU / tên / tổng tồn) trên SQL Server thật; file xuất Excel theo cùng thứ tự.</summary>
[Collection(ApiCollection.Name)]
public class StockSearchTests(InventoryApiFactory factory)
{
    /// <summary>4 sản phẩm chung tiền tố SKU: A=5, B=50, C=20 (kho A), D chưa có tồn.</summary>
    private async Task<(HttpClient Client, string Prefix)> SeedAsync()
    {
        var admin = await factory.CreateClientForAsync("admin@inventory.local", "Admin@123");
        var groupId = (await admin.GetFromJsonAsync<JsonArray>("/api/v1/product-groups"))![0]!["id"]!.GetValue<int>();
        var prefix = $"SRT{Guid.NewGuid():N}"[..14].ToUpperInvariant();

        foreach (var (suffix, name, qty) in new[] { ("A", "Sort Zeta", 5m), ("B", "Sort Alpha", 50m), ("C", "Sort Mid", 20m), ("D", "Sort Beta", 0m) })
        {
            var created = await admin.PostAsJsonAsync("/api/v1/products", new
            {
                sku = $"{prefix}-{suffix}", name, unit = "cái", groupId, cost = 1000, price = 1500
            });
            created.EnsureSuccessStatusCode();
            if (qty == 0) continue;
            var productId = JsonNode.Parse(await created.Content.ReadAsStringAsync())!["id"]!.GetValue<int>();
            var receipt = await admin.PostAsJsonAsync("/api/v1/goods-receipts", new
            {
                warehouseId = 1, supplierId = 1, post = true,
                lines = new[] { new { productId, quantity = qty, unitCost = 1000 } }
            });
            Assert.Equal(HttpStatusCode.Created, receipt.StatusCode);
        }
        return (admin, prefix);
    }

    private static async Task<string[]> SuffixesAsync(HttpClient client, string prefix, string query)
    {
        var page = (await client.GetFromJsonAsync<JsonObject>($"/api/v1/stock?search={prefix}&{query}"))!;
        return page["items"]!.AsArray().Select(i => i!["sku"]!.GetValue<string>()[(prefix.Length + 1)..]).ToArray();
    }

    [Fact]
    public async Task Sorts_by_sku_name_or_total_with_stable_paging()
    {
        var (client, prefix) = await SeedAsync();

        Assert.Equal(["A", "B", "C", "D"], await SuffixesAsync(client, prefix, "pageSize=10"));
        Assert.Equal(["B", "D", "C", "A"], await SuffixesAsync(client, prefix, "sort=Name"));
        Assert.Equal(["B", "C", "A", "D"], await SuffixesAsync(client, prefix, "sort=TotalDesc"));
        Assert.Equal(["D", "A", "C", "B"], await SuffixesAsync(client, prefix, "sort=TotalAsc"));
        // Trang 2 nối tiếp đúng trang 1 khi sắp theo tổng.
        Assert.Equal(["A", "D"], await SuffixesAsync(client, prefix, "sort=TotalDesc&pageSize=2&page=2"));

        var invalid = await client.GetAsync($"/api/v1/stock?sort=Bogus");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task Stock_export_follows_the_screen_sort()
    {
        var (client, prefix) = await SeedAsync();

        var file = await client.GetAsync($"/api/v1/exports/stock?search={prefix}&sort=TotalDesc");
        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
        using var wb = new XLWorkbook(await file.Content.ReadAsStreamAsync());
        var skus = wb.Worksheet(1).RowsUsed().Select(r => r.Cell(1).GetString()).Where(s => s.StartsWith(prefix)).ToArray();
        Assert.Equal([$"{prefix}-B", $"{prefix}-C", $"{prefix}-A", $"{prefix}-D"], skus);
    }
}
