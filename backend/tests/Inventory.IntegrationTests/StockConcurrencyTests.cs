using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Inventory.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Inventory.IntegrationTests;

/// <summary>
/// Definition of Done của Dự án #2, chạy trên SQL Server thật (rowversion, CHECK constraint, unique index là thật):
/// - Nhiều phiếu xuất đồng thời trên hàng sắp hết → chỉ số phiếu vừa đủ hàng thành công, còn lại 409; tồn không âm.
/// - Mọi thay đổi tồn có StockMovement tương ứng (tổng sổ cái = tồn).
/// - Chuyển kho nguyên tử; Idempotency-Key chống tạo trùng kể cả khi gửi song song.
/// </summary>
[Collection(ApiCollection.Name)]
public class StockConcurrencyTests(InventoryApiFactory factory)
{
    private const int WarehouseA = 1;
    private const int WarehouseC = 3;

    private Task<HttpClient> ManagerAsync() => factory.CreateClientForAsync("manager@inventory.local", "Manager@123");

    /// <summary>Tạo SKU riêng cho từng test (tránh phụ thuộc dữ liệu giữa các test) và nhập sẵn <paramref name="openingQty"/> vào kho A.</summary>
    private async Task<int> NewProductWithStockAsync(decimal openingQty)
    {
        var admin = await factory.CreateClientForAsync("admin@inventory.local", "Admin@123");
        var groups = await admin.GetFromJsonAsync<JsonArray>("/api/v1/product-groups");
        var created = await admin.PostAsJsonAsync("/api/v1/products", new
        {
            sku = $"IT-{Guid.NewGuid():N}"[..20], name = "Integration item", unit = "cái",
            groupId = groups![0]!["id"]!.GetValue<int>(), cost = 1000, price = 1500
        });
        created.EnsureSuccessStatusCode();
        var productId = JsonNode.Parse(await created.Content.ReadAsStringAsync())!["id"]!.GetValue<int>();

        var receipt = await admin.PostAsJsonAsync("/api/v1/goods-receipts", new
        {
            warehouseId = WarehouseA, supplierId = 1, post = true,
            lines = new[] { new { productId, quantity = openingQty, unitCost = 1000 } }
        });
        Assert.Equal(HttpStatusCode.Created, receipt.StatusCode);
        return productId;
    }

    private async Task<(decimal Level, decimal LedgerSum, int Movements)> StockOfAsync(int productId, int warehouseId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var level = await db.StockLevels.Where(s => s.ProductId == productId && s.WarehouseId == warehouseId)
            .Select(s => s.Quantity).SingleOrDefaultAsync();
        var moves = await db.StockMovements.Where(m => m.ProductId == productId && m.WarehouseId == warehouseId).ToListAsync();
        return (level, moves.Sum(m => m.Quantity), moves.Count);
    }

    [Fact]
    public async Task Concurrent_issues_on_low_stock_never_oversell()
    {
        var productId = await NewProductWithStockAsync(10);
        var clients = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => ManagerAsync()));

        // 6 phiếu × 3 cái trên tồn 10 → chỉ 3 phiếu đủ hàng.
        var responses = await Task.WhenAll(clients.Select(c => c.PostAsJsonAsync("/api/v1/goods-issues", new
        {
            warehouseId = WarehouseA, reason = "Sale", post = true,
            lines = new[] { new { productId, quantity = 3 } }
        })));

        Assert.Equal(3, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        var rejected = responses.Where(r => r.StatusCode != HttpStatusCode.Created).ToList();
        Assert.All(rejected, r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        var problem = JsonNode.Parse(await rejected[0].Content.ReadAsStringAsync())!;
        Assert.Equal(1, problem["shortages"]![0]!["available"]!.GetValue<decimal>());

        var (level, ledgerSum, movements) = await StockOfAsync(productId, WarehouseA);
        Assert.Equal(1, level);
        Assert.Equal(level, ledgerSum);
        Assert.Equal(1 + 3, movements); // nhập đầu kỳ + 3 phiếu xuất
    }

    [Fact]
    public async Task Parallel_requests_with_same_idempotency_key_create_one_document()
    {
        var productId = await NewProductWithStockAsync(50);
        var client = await ManagerAsync();
        var key = $"it-{Guid.NewGuid():N}";

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/goods-issues")
            {
                Content = JsonContent.Create(new { warehouseId = WarehouseA, reason = "Sale", post = true, lines = new[] { new { productId, quantity = 5 } } })
            };
            request.Headers.Add("Idempotency-Key", key);
            return client.SendAsync(request);
        }));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        var ids = await Task.WhenAll(responses.Select(async r => JsonNode.Parse(await r.Content.ReadAsStringAsync())!["id"]!.GetValue<int>()));
        Assert.Single(ids.Distinct());
        Assert.Equal(45, (await StockOfAsync(productId, WarehouseA)).Level);
    }

    [Fact]
    public async Task Transfer_is_atomic_when_one_line_is_short()
    {
        var productId = await NewProductWithStockAsync(20);
        var client = await ManagerAsync();

        var failed = await client.PostAsJsonAsync("/api/v1/transfers", new
        {
            fromWarehouseId = WarehouseA, toWarehouseId = WarehouseC, post = true,
            lines = new[] { new { productId, quantity = 5m }, new { productId = 1, quantity = 1_000_000m } }
        });
        Assert.Equal(HttpStatusCode.Conflict, failed.StatusCode);
        Assert.Equal((20m, 0m), ((await StockOfAsync(productId, WarehouseA)).Level, (await StockOfAsync(productId, WarehouseC)).Level));

        var ok = await client.PostAsJsonAsync("/api/v1/transfers", new
        {
            fromWarehouseId = WarehouseA, toWarehouseId = WarehouseC, post = true,
            lines = new[] { new { productId, quantity = 5m } }
        });
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        Assert.Equal((15m, 5m), ((await StockOfAsync(productId, WarehouseA)).Level, (await StockOfAsync(productId, WarehouseC)).Level));
    }

    [Fact]
    public async Task Staff_drafts_but_cannot_approve_and_manager_posts_with_rowversion()
    {
        var productId = await NewProductWithStockAsync(8);
        var staff = await factory.CreateClientForAsync("staff@inventory.local", "Staff@123");
        var manager = await ManagerAsync();

        var draft = await staff.PostAsJsonAsync("/api/v1/goods-issues", new
        {
            warehouseId = WarehouseA, reason = "Disposal", lines = new[] { new { productId, quantity = 2 } }
        });
        Assert.Equal(HttpStatusCode.Created, draft.StatusCode);
        var doc = JsonNode.Parse(await draft.Content.ReadAsStringAsync())!;
        var id = doc["id"]!.GetValue<int>();
        var rowVersion = doc["rowVersion"]!.GetValue<string>();

        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsJsonAsync($"/api/v1/goods-issues/{id}/post", new { rowVersion })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.PostAsJsonAsync($"/api/v1/goods-receipts/{id}/post", new { rowVersion })).StatusCode);

        var posted = await manager.PostAsJsonAsync($"/api/v1/goods-issues/{id}/post", new { rowVersion });
        Assert.Equal(HttpStatusCode.OK, posted.StatusCode);
        Assert.Equal(6, (await StockOfAsync(productId, WarehouseA)).Level);

        // rowVersion cũ → 409; phiếu đã ghi sổ không hủy được.
        Assert.Equal(HttpStatusCode.Conflict, (await manager.PostAsJsonAsync($"/api/v1/goods-issues/{id}/post", new { rowVersion })).StatusCode);
        var fresh = JsonNode.Parse(await posted.Content.ReadAsStringAsync())!["rowVersion"]!.GetValue<string>();
        Assert.Equal(HttpStatusCode.Conflict, (await manager.PostAsJsonAsync($"/api/v1/goods-issues/{id}/cancel", new { rowVersion = fresh })).StatusCode);
    }

    [Fact]
    public async Task Database_rejects_negative_stock_even_if_code_is_bypassed()
    {
        var productId = await NewProductWithStockAsync(1);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        var ex = await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE StockLevels SET Quantity = -1 WHERE ProductId = {productId} AND WarehouseId = {WarehouseA}"));
        Assert.Contains("CK_StockLevels_Quantity_NonNegative", ex.Message);
    }
}
