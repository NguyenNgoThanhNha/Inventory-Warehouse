using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace Inventory.IntegrationTests;

/// <summary>usp_Report_Kardex / usp_Report_Dashboard chạy trên SQL Server thật (RULES 3.10f).</summary>
[Collection(ApiCollection.Name)]
public class ReportTests(InventoryApiFactory factory)
{
    private const int WarehouseA = 1;
    private const int WarehouseC = 3;

    private static async Task<JsonNode> PostOkAsync(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
    }

    /// <summary>Sản phẩm mới + 4 nghiệp vụ ở kho A: nhập 50, xuất 10, chuyển 5 sang C, kiểm kê đếm được 30 (lệch −5).</summary>
    private async Task<(HttpClient Client, int ProductId)> SeedMovementsAsync()
    {
        var admin = await factory.CreateClientForAsync("admin@inventory.local", "Admin@123");
        var groups = await admin.GetFromJsonAsync<JsonArray>("/api/v1/product-groups");
        var product = await PostOkAsync(admin, "/api/v1/products", new
        {
            sku = $"RPT-{Guid.NewGuid():N}"[..20], name = "Report item", unit = "cái",
            groupId = groups![0]!["id"]!.GetValue<int>(), cost = 2000, price = 3000
        });
        var productId = product["id"]!.GetValue<int>();
        var line = (decimal qty) => new[] { new { productId, quantity = qty } };

        await PostOkAsync(admin, "/api/v1/goods-receipts", new { warehouseId = WarehouseA, supplierId = 1, post = true, lines = line(50) });
        await PostOkAsync(admin, "/api/v1/goods-issues", new { warehouseId = WarehouseA, reason = "Sale", post = true, lines = line(10) });
        await PostOkAsync(admin, "/api/v1/transfers", new { fromWarehouseId = WarehouseA, toWarehouseId = WarehouseC, post = true, lines = line(5) });
        await PostOkAsync(admin, "/api/v1/stock-takes", new { warehouseId = WarehouseA, post = true, lines = line(30) });
        return (admin, productId);
    }

    private static decimal Dec(JsonNode? node) => node!.GetValue<decimal>();

    [Fact]
    public async Task Kardex_running_balance_matches_stock_and_survives_paging()
    {
        var (client, productId) = await SeedMovementsAsync();

        var kardex = (await client.GetFromJsonAsync<JsonObject>($"/api/v1/reports/kardex?productId={productId}&warehouseId={WarehouseA}"))!;
        Assert.Equal(0m, Dec(kardex["opening"]));
        Assert.Equal(50m, Dec(kardex["totalIn"]));
        Assert.Equal(20m, Dec(kardex["totalOut"])); // 10 xuất + 5 chuyển đi + 5 lệch kiểm kê
        Assert.Equal(30m, Dec(kardex["closing"]));
        var rows = kardex["rows"]!.AsArray();
        Assert.Equal([50m, 40m, 35m, 30m], rows.Select(r => Dec(r!["balance"])));
        Assert.Equal(["In", "Out", "TransferOut", "Adjust"], rows.Select(r => r!["movementType"]!.GetValue<string>()));
        Assert.EndsWith("Z", rows[0]!["occurredAt"]!.GetValue<string>());

        // Trang 2 vẫn mang số dư lũy kế từ trang 1 (running total tính trước khi phân trang).
        var page2 = (await client.GetFromJsonAsync<JsonObject>(
            $"/api/v1/reports/kardex?productId={productId}&warehouseId={WarehouseA}&page=2&pageSize=2"))!;
        Assert.Equal(4, page2["totalCount"]!.GetValue<int>());
        Assert.Equal([35m, 30m], page2["rows"]!.AsArray().Select(r => Dec(r!["balance"])));

        var available = await client.GetFromJsonAsync<JsonArray>($"/api/v1/stock/available?warehouseId={WarehouseA}&productIds={productId}");
        Assert.Equal(Dec(kardex["closing"]), Dec(available![0]!["quantity"]));

        // Mọi kho: chuyển kho nội bộ hiện cả 2 chiều, tồn cuối = 30 (A) + 5 (C).
        var all = (await client.GetFromJsonAsync<JsonObject>($"/api/v1/reports/kardex?productId={productId}"))!;
        Assert.Equal(5, all["totalCount"]!.GetValue<int>());
        Assert.Equal(35m, Dec(all["closing"]));
    }

    [Fact]
    public async Task Kardex_before_the_range_moves_into_the_opening_balance()
    {
        var (client, productId) = await SeedMovementsAsync();
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)).AddDays(1).ToString("yyyy-MM-dd");

        var kardex = (await client.GetFromJsonAsync<JsonObject>(
            $"/api/v1/reports/kardex?productId={productId}&warehouseId={WarehouseA}&from={tomorrow}&to={tomorrow}"))!;

        Assert.Equal(30m, Dec(kardex["opening"]));
        Assert.Equal(30m, Dec(kardex["closing"]));
        Assert.Empty(kardex["rows"]!.AsArray());

        var invalid = await client.GetAsync($"/api/v1/reports/kardex?productId={productId}&from=2020-01-01&to=2026-01-01");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task Dashboard_aggregates_in_sql()
    {
        await SeedMovementsAsync();
        var manager = await factory.CreateClientForAsync("manager@inventory.local", "Manager@123");

        var dashboard = (await manager.GetFromJsonAsync<JsonObject>($"/api/v1/reports/dashboard?warehouseId={WarehouseA}"))!;

        Assert.True(Dec(dashboard["stockValue"]) > 0);
        Assert.True(dashboard["postedToday"]!.GetValue<int>() >= 4);
        var byWarehouse = dashboard["valueByWarehouse"]!.AsArray();
        Assert.Equal(WarehouseA, byWarehouse.Single()!["warehouseId"]!.GetValue<int>());
        Assert.Equal(Dec(dashboard["stockValue"]), Dec(byWarehouse[0]!["value"]));
        Assert.Equal(Dec(dashboard["stockValue"]), dashboard["valueByGroup"]!.AsArray().Sum(g => Dec(g!["value"])));
        var days = dashboard["inOutByDay"]!.AsArray();
        Assert.Equal(30, days.Count);
        Assert.True(Dec(days[^1]!["inValue"]) > 0); // hôm nay có phiếu nhập
        Assert.NotNull(dashboard["generatedAt"]);
    }

    [Fact]
    public async Task Reports_require_STOCK_REPORT_read()
    {
        var admin = await factory.CreateClientForAsync("admin@inventory.local", "Admin@123");
        var register = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/register",
            new { email = $"rpt{Guid.NewGuid():N}@test.local", password = "Secret@123", fullName = "No report" });
        var auth = JsonNode.Parse(await register.Content.ReadAsStringAsync())!;
        (await admin.PutAsJsonAsync($"/api/v1/users/{auth["user"]!["id"]}/roles", new { roleIds = Array.Empty<Guid>() })).EnsureSuccessStatusCode();
        var user = factory.CreateClient();
        user.DefaultRequestHeaders.Authorization = new("Bearer", auth["accessToken"]!.GetValue<string>());

        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/api/v1/reports/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/api/v1/reports/kardex?productId=1")).StatusCode);
    }
}
