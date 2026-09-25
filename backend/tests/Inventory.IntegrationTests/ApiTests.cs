using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Inventory.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ApiTests(InventoryApiFactory factory)
{
    private const string StaffEmail = "staff@inventory.local";
    private const string StaffPassword = "Staff@123";

    [Fact]
    public async Task Login_returns_effective_permissions()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "manager@inventory.local", password = "Manager@123" });

        response.EnsureSuccessStatusCode();
        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        var permissions = json["user"]!["permissions"]!.AsArray();
        Assert.Contains(permissions, p => p!["code"]!.GetValue<string>() == "GOODS_ISSUE" && p["u"]!.GetValue<bool>());
        Assert.False(json["user"]!["isAdmin"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Wrong_password_returns_401_problem_details()
    {
        var response = await factory.CreateClient()
            .PostAsJsonAsync("/api/v1/auth/login", new { email = StaffEmail, password = "wrong" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Staff_is_forbidden_on_admin_and_catalog_write_endpoints()
    {
        var client = await factory.CreateClientForAsync(StaffEmail, StaffPassword);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/v1/product-groups", new { name = "X" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/api-logs")).StatusCode);
    }

    [Fact]
    public async Task Per_user_permission_takes_effect_immediately_and_api_is_logged()
    {
        var admin = await factory.CreateClientForAsync("admin@inventory.local", "Admin@123");
        var register = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/register",
            new { email = $"it{Guid.NewGuid():N}@test.local", password = "Secret@123", fullName = "IT user" });
        var auth = JsonNode.Parse(await register.Content.ReadAsStringAsync())!;
        var user = factory.CreateClient();
        user.DefaultRequestHeaders.Authorization = new("Bearer", auth["accessToken"]!.GetValue<string>());

        var denied = await user.GetAsync("/api/v1/users");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var traceId = JsonNode.Parse(await denied.Content.ReadAsStringAsync())!["traceId"]!.GetValue<string>();

        var activities = await admin.GetFromJsonAsync<JsonArray>("/api/v1/activities");
        var userActivityId = activities!.First(a => a!["code"]!.GetValue<string>() == "USER")!["id"]!.GetValue<string>();
        var grant = await admin.PutAsJsonAsync($"/api/v1/users/{auth["user"]!["id"]}/permissions",
            new { activities = new[] { new { activityId = userActivityId, c = false, r = true, u = false, d = false } } });
        grant.EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync("/api/v1/users")).StatusCode);

        // Log API được ghi nền theo lô → chờ tối đa ~10 giây.
        JsonObject? logs = null;
        for (var i = 0; i < 20 && (logs?["totalCount"]?.GetValue<int>() ?? 0) == 0; i++)
        {
            await Task.Delay(500);
            logs = await admin.GetFromJsonAsync<JsonObject>($"/api/v1/api-logs?traceId={Uri.EscapeDataString(traceId)}");
        }
        Assert.Equal(1, logs!["totalCount"]!.GetValue<int>());

        var registerLog = await admin.GetFromJsonAsync<JsonObject>("/api/v1/api-logs?url=/auth/register&pageSize=1");
        var detail = await admin.GetFromJsonAsync<JsonObject>($"/api/v1/api-logs/{registerLog!["items"]![0]!["id"]}");
        Assert.DoesNotContain("Secret@123", detail!["request"]!.GetValue<string>());
    }
}
