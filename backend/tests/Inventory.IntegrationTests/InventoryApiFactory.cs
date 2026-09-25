using System.Net.Http.Headers;
using System.Net.Http.Json;
using Inventory.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace Inventory.IntegrationTests;

/// <summary>
/// Chạy API thật với SQL Server thật:
/// - Có biến môi trường TEST_SQL_CONNECTION (vd: "Server=.\MSSQLSERVER01;Trusted_Connection=True;TrustServerCertificate=True")
///   → dùng SQL Server có sẵn, tạo database riêng cho mỗi lần chạy rồi xóa.
/// - Không có → khởi động SQL Server bằng Testcontainers (cần Docker) — dùng trong CI.
/// </summary>
public sealed class InventoryApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private MsSqlContainer? _container;
    private string _connectionString = string.Empty;

    public async Task InitializeAsync()
    {
        var external = Environment.GetEnvironmentVariable("TEST_SQL_CONNECTION");
        if (!string.IsNullOrWhiteSpace(external))
        {
            _connectionString = new SqlConnectionStringBuilder(external) { InitialCatalog = $"InventoryTest_{Guid.NewGuid():N}" }.ConnectionString;
        }
        else
        {
            _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
            await _container.StartAsync();
            _connectionString = new SqlConnectionStringBuilder(_container.GetConnectionString()) { InitialCatalog = "InventoryTest" }.ConnectionString;
        }

        _ = Server; // khởi động host → migrate + seed
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", _connectionString);
        builder.UseSetting("Jwt:Key", "integration-test-secret-key-0123456789abcdef");
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Database:SeedDemoData", "true");
        builder.UseSetting("RateLimiting:AuthPermitPerMinute", "1000");
        builder.UseSetting("StockAlerts:InitialDelaySeconds", "3600"); // test gọi /stock-alerts/scan trực tiếp
        builder.UseSetting("FileStorage:RootPath", Path.Combine(Path.GetTempPath(), "inventory-test-uploads"));
    }

    public async Task<HttpClient> CreateClientForAsync(string email, string password)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginResult>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return client;
    }

    public new async Task DisposeAsync()
    {
        if (_container is null)
        {
            using var scope = Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<InventoryDbContext>().Database.EnsureDeletedAsync();
        }
        await base.DisposeAsync();
        if (_container is not null) await _container.DisposeAsync();
    }

    private sealed record LoginResult(string AccessToken);
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<InventoryApiFactory>
{
    public const string Name = "api";
}
