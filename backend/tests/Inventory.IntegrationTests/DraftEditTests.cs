using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace Inventory.IntegrationTests;

/// <summary>Sửa phiếu nháp: thay dòng hàng, phân quyền theo chủ phiếu, rowVersion, phiếu đã ghi sổ là bất biến.</summary>
[Collection(ApiCollection.Name)]
public class DraftEditTests(InventoryApiFactory factory)
{
    private static async Task<JsonNode> ReadAsync(HttpResponseMessage response) =>
        JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

    private async Task<(HttpClient Staff, JsonNode Draft)> DraftAsync()
    {
        var staff = await factory.CreateClientForAsync("staff@inventory.local", "Staff@123");
        var created = await staff.PostAsJsonAsync("/api/v1/goods-issues", new
        {
            warehouseId = 1, reason = "Sale", note = "v1",
            lines = new[] { new { productId = 1, quantity = 3 }, new { productId = 2, quantity = 1 } }
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return (staff, await ReadAsync(created));
    }

    [Fact]
    public async Task Owner_replaces_lines_and_header_of_a_draft()
    {
        var (staff, draft) = await DraftAsync();
        var id = draft["id"]!.GetValue<int>();

        // Giữ lại sản phẩm 1 (đổi số lượng) → dòng cũ phải bị xóa trước khi thêm dòng mới (unique DocumentId+ProductId).
        var updated = await staff.PutAsJsonAsync($"/api/v1/goods-issues/{id}", new
        {
            rowVersion = draft["rowVersion"]!.GetValue<string>(), warehouseId = 1, reason = "Disposal", note = "v2",
            lines = new[] { new { productId = 1, quantity = 7 } }
        });

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var doc = await ReadAsync(updated);
        Assert.Equal(("Disposal", "v2", "Draft"), (doc["reason"]!.GetValue<string>(), doc["note"]!.GetValue<string>(), doc["status"]!.GetValue<string>()));
        var line = doc["lines"]!.AsArray().Single()!;
        Assert.Equal((1, 7m), (line["productId"]!.GetValue<int>(), line["quantity"]!.GetValue<decimal>()));
        Assert.Equal(draft["code"]!.GetValue<string>(), doc["code"]!.GetValue<string>()); // vẫn là phiếu cũ

        // rowVersion cũ → 409
        var stale = await staff.PutAsJsonAsync($"/api/v1/goods-issues/{id}", new
        {
            rowVersion = draft["rowVersion"]!.GetValue<string>(), warehouseId = 1, reason = "Sale",
            lines = new[] { new { productId = 1, quantity = 1 } }
        });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [Fact]
    public async Task Other_staff_cannot_edit_but_manager_can_and_posted_documents_are_final()
    {
        var (_, draft) = await DraftAsync();
        var id = draft["id"]!.GetValue<int>();
        var body = new
        {
            rowVersion = draft["rowVersion"]!.GetValue<string>(), warehouseId = 1, reason = "Sale",
            lines = new[] { new { productId = 1, quantity = 2 } }
        };

        var otherStaff = await factory.CreateClientForAsync("staff2@inventory.local", "Staff@123");
        Assert.Equal(HttpStatusCode.Forbidden, (await otherStaff.PutAsJsonAsync($"/api/v1/goods-issues/{id}", body)).StatusCode);

        var manager = await factory.CreateClientForAsync("manager@inventory.local", "Manager@123");
        var edited = await manager.PutAsJsonAsync($"/api/v1/goods-issues/{id}", body);
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);

        var rowVersion = (await ReadAsync(edited))["rowVersion"]!.GetValue<string>();
        var posted = await manager.PostAsJsonAsync($"/api/v1/goods-issues/{id}/post", new { rowVersion });
        Assert.Equal(HttpStatusCode.OK, posted.StatusCode);

        var afterPost = await manager.PutAsJsonAsync($"/api/v1/goods-issues/{id}", body with { rowVersion = (await ReadAsync(posted))["rowVersion"]!.GetValue<string>() });
        Assert.Equal(HttpStatusCode.Conflict, afterPost.StatusCode);
    }

    [Fact]
    public async Task Validation_follows_the_document_type()
    {
        var (staff, draft) = await DraftAsync();
        var response = await staff.PutAsJsonAsync($"/api/v1/goods-issues/{draft["id"]}", new
        {
            rowVersion = draft["rowVersion"]!.GetValue<string>(), warehouseId = 1, reason = (string?)null,
            lines = Array.Empty<object>()
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await ReadAsync(response))["errors"]!.AsObject();
        Assert.True(errors.ContainsKey("reason"));
        Assert.True(errors.ContainsKey("lines"));
    }
}
