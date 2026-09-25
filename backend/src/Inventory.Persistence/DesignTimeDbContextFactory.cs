using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Inventory.Persistence;

/// <summary>
/// Cho lệnh `dotnet ef` tạo DbContext mà không khởi động Api (không chạy seed, background job...).
/// Connection string lấy từ biến môi trường INVENTORY_DESIGN_CONNECTION (chỉ cần khi `database update`).
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<InventoryDbContext>
{
    public InventoryDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("INVENTORY_DESIGN_CONNECTION")
                         ?? "Server=.\\MSSQLSERVER01;Database=InventoryDb;Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlServer(connection).Options;
        return new InventoryDbContext(options);
    }
}
