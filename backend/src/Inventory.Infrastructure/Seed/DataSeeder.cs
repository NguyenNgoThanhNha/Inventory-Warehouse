using Inventory.Application.Common.Interfaces;
using Inventory.Application.Features.V1.StockDocuments.Services;
using Inventory.Domain.Constants;
using Inventory.Domain.Entities.Catalog;
using Inventory.Domain.Entities.Stock;
using Inventory.Domain.Entities.Sys;
using Inventory.Domain.Enums;
using Inventory.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Inventory.Infrastructure.Seed;

/// <summary>
/// Seed dữ liệu (chạy mỗi lần khởi động, idempotent):
/// - Đồng bộ ConstActivity.All → Sys_Activity (thêm mã mới, cập nhật tên; không xóa mã cũ).
/// - Role hệ thống Admin / Manager / Staff + quyền mặc định (chỉ khi role mới được tạo).
/// - Khi <c>includeDemoData</c>: tài khoản demo, danh mục mẫu và phiếu nhập tồn đầu kỳ (đi qua StockLedger như phiếu thật).
/// </summary>
public sealed class DataSeeder(
    IUnitOfWork<InventoryDbContext> unitOfWork,
    IPasswordHasher hasher,
    IDocumentNumberGenerator numbers,
    IStockDocumentPoster poster,
    ILogger<DataSeeder> logger)
{
    public async Task SeedAsync(bool includeDemoData, CancellationToken ct = default)
    {
        var activities = await SyncActivitiesAsync(ct);
        var roles = await EnsureRolesAsync(activities, ct);
        if (includeDemoData)
        {
            await EnsureDemoUsersAsync(roles, ct);
            await EnsureDemoCatalogAsync(ct);
        }
        await unitOfWork.SaveChangesAsync(ct);

        // Tồn đầu kỳ cần Id sản phẩm/kho đã sinh ở lần lưu trên → lưu lần hai (seeder không phải handler nghiệp vụ).
        if (includeDemoData && await EnsureOpeningStockAsync(ct)) await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation("Data seeding completed");
    }

    private const string BulkSkuPrefix = "BULK-";

    /// <summary>
    /// Dữ liệu lớn để thử bảng tồn kho / đo query (DoD: bảng mượt với ≥ 10.000 dòng). Chỉ chạy khi cấu hình
    /// <c>Database:SeedBulkProducts</c> &gt; 0. Tồn đầu kỳ vẫn đi qua StockLedger (có StockMovement như phiếu thật).
    /// </summary>
    public async Task SeedBulkProductsAsync(int count, CancellationToken ct = default)
    {
        if (count <= 0) return;
        var existing = await unitOfWork.Repository<Product>().CountAsync(p => p.Sku.StartsWith(BulkSkuPrefix), ct);
        if (existing >= count) return;

        var groups = await unitOfWork.Repository<ProductGroup>().OrderBy(g => g.Id).ToListAsync(ct);
        var warehouses = await unitOfWork.Repository<Warehouse>().OrderBy(w => w.Code).Take(2).ToListAsync(ct);
        var supplier = await unitOfWork.Repository<Supplier>().OrderBy(s => s.Id).FirstOrDefaultAsync(ct);
        if (groups.Count == 0 || warehouses.Count == 0 || supplier is null) return; // cần danh mục demo trước

        var products = Enumerable.Range(existing + 1, count - existing)
            .Select(i => new Product($"{BulkSkuPrefix}{i:D6}", $"Hàng mẫu số {i}", i % 3 == 0 ? "hộp" : "cái",
                groups[i % groups.Count].Id, 1_000 + i % 500 * 100, 1_500 + i % 500 * 150))
            .ToList();
        unitOfWork.Repository<Product>().AddRange(products);
        await unitOfWork.SaveChangesAsync(ct);

        for (var w = 0; w < warehouses.Count; w++)
        {
            var receipt = StockDocument.NewReceipt(await numbers.NextAsync(DocumentType.GoodsReceipt, ct),
                warehouses[w].Id, supplier.Id, $"Tồn đầu kỳ dữ liệu lớn ({products.Count} mã)");
            for (var i = 0; i < products.Count; i++)
                receipt.AddLine(products[i].Id, (i * 31 + w * 17) % 900 + 1, products[i].Cost);
            unitOfWork.Repository<StockDocument>().Add(receipt);
            await poster.PostAsync(receipt, ct);
        }
        foreach (var level in unitOfWork.Repository<StockLevel>().Local.Where(l => l.ProductId >= products[0].Id))
            level.SetMinThreshold(level.ProductId % 7 == 0 ? 500 : 0);

        await unitOfWork.SaveChangesAsync(ct);
        logger.LogInformation("Seeded {Count} bulk products", products.Count);
    }

    private async Task<Dictionary<string, SysActivity>> SyncActivitiesAsync(CancellationToken ct)
    {
        var set = unitOfWork.Repository<SysActivity>();
        var existing = await set.ToDictionaryAsync(a => a.Code, StringComparer.OrdinalIgnoreCase, ct);

        foreach (var def in ConstActivity.All)
        {
            if (existing.TryGetValue(def.Code, out var activity))
            {
                activity.Name = def.Name;
                activity.Description = def.Description;
            }
            else
            {
                activity = new SysActivity
                {
                    Code = def.Code, Name = def.Name, Description = def.Description, ApplicationName = ConstActivity.ApplicationName
                };
                set.Add(activity);
                existing[def.Code] = activity;
            }
        }
        return existing;
    }

    private async Task<Dictionary<string, SysRole>> EnsureRolesAsync(Dictionary<string, SysActivity> activities, CancellationToken ct)
    {
        var set = unitOfWork.Repository<SysRole>();
        var roles = await set.ToDictionaryAsync(r => r.Name, StringComparer.OrdinalIgnoreCase, ct);

        if (!roles.ContainsKey(ConstRole.Admin))
        {
            var admin = new SysRole { Name = ConstRole.Admin, Description = "Quản trị hệ thống — toàn quyền", RoleType = ConstRole.AdminRoleType };
            set.Add(admin);
            roles[admin.Name] = admin;
        }

        foreach (var (roleName, permissions) in ConstRole.DefaultPermissions)
        {
            if (roles.ContainsKey(roleName)) continue; // role đã có → không ghi đè cấu hình admin đã chỉnh

            var role = new SysRole { Name = roleName, Description = $"Role mặc định: {roleName}" };
            set.Add(role);
            roles[roleName] = role;

            foreach (var (code, flags) in permissions)
            {
                var ra = new SysRoleActivity { RoleId = role.Id, ActivityId = activities[code].Id };
                ra.SetFlags(flags.Contains('C'), flags.Contains('R'), flags.Contains('U'), flags.Contains('D'));
                unitOfWork.Repository<SysRoleActivity>().Add(ra);
            }
        }
        return roles;
    }

    private async Task EnsureDemoUsersAsync(Dictionary<string, SysRole> roles, CancellationToken ct)
    {
        (string Email, string Name, string Password, string Role)[] demo =
        [
            ("admin@inventory.local", "Quản trị viên", "Admin@123", ConstRole.Admin),
            ("manager@inventory.local", "Lê Quản Lý", "Manager@123", ConstRole.Manager),
            ("staff@inventory.local", "Phạm Thủ Kho", "Staff@123", ConstRole.Staff),
            ("staff2@inventory.local", "Võ Thủ Kho", "Staff@123", ConstRole.Staff)
        ];

        var existing = await unitOfWork.Repository<SysAccount>().Select(u => u.Email).ToListAsync(ct);
        foreach (var (email, name, password, roleName) in demo.Where(d => !existing.Contains(d.Email)))
        {
            var user = new SysAccount { Email = email, FullName = name };
            user.SetPasswordHash(hasher.Hash(user, password));
            unitOfWork.Repository<SysAccount>().Add(user);
            unitOfWork.Repository<SysUserRole>().Add(new SysUserRole { UserId = user.Id, RoleId = roles[roleName].Id });
        }
    }

    private static readonly (string Group, (string Sku, string Name, string Unit, decimal Cost, decimal Price)[] Items)[] DemoProducts =
    [
        ("Ốc vít & bu lông",
        [
            ("P001", "Ốc vít M6", "cái", 500, 800), ("P004", "Bu lông M8x40", "cái", 1_200, 1_900),
            ("P005", "Đai ốc M8", "cái", 300, 500), ("P006", "Vít gỗ 4x30", "hộp", 25_000, 38_000),
            ("P007", "Tắc kê nhựa 8mm", "túi", 12_000, 18_000)
        ]),
        ("Phụ kiện cửa",
        [
            ("P002", "Bản lề inox", "cái", 35_000, 52_000), ("P003", "Tay nắm cửa", "cái", 85_000, 120_000),
            ("P008", "Khóa tay gạt", "bộ", 320_000, 450_000), ("P009", "Chặn cửa nam châm", "cái", 45_000, 65_000),
            ("P010", "Ray trượt ngăn kéo 45cm", "cặp", 90_000, 135_000)
        ]),
        ("Dụng cụ cầm tay",
        [
            ("P011", "Tua vít bake", "cây", 28_000, 42_000), ("P012", "Kìm điện 8 inch", "cây", 95_000, 140_000),
            ("P013", "Thước cuộn 5m", "cái", 40_000, 60_000), ("P014", "Búa đóng đinh", "cây", 110_000, 160_000)
        ]),
        ("Vật tư điện",
        [
            ("P015", "Dây điện 2x1.5", "m", 9_000, 13_000), ("P016", "Ổ cắm đôi", "cái", 38_000, 55_000),
            ("P017", "Công tắc 1 chiều", "cái", 22_000, 33_000), ("P018", "Băng keo điện", "cuộn", 6_000, 10_000)
        ])
    ];

    private async Task EnsureDemoCatalogAsync(CancellationToken ct)
    {
        if (await unitOfWork.Repository<Product>().AnyAsync(ct)) return;

        unitOfWork.Repository<Warehouse>().AddRange(
            new Warehouse("KHO-A", "Kho A — TP.HCM", "Quận 7, TP.HCM"),
            new Warehouse("KHO-B", "Kho B — Hà Nội", "Long Biên, Hà Nội"),
            new Warehouse("KHO-C", "Kho C — Đà Nẵng", "Liên Chiểu, Đà Nẵng"));

        unitOfWork.Repository<Supplier>().AddRange(
            new Supplier("Công ty Kim khí Thành Phát", "0283 555 0101", "sales@thanhphat.example", "Bình Tân, TP.HCM"),
            new Supplier("Điện Quang Minh", "0243 555 0202", null, "Hoàng Mai, Hà Nội"));

        var groups = DemoProducts.Select(g => new ProductGroup(g.Group)).ToList();
        unitOfWork.Repository<ProductGroup>().AddRange(groups);
        await unitOfWork.SaveChangesAsync(ct); // cần Id nhóm hàng cho sản phẩm

        for (var g = 0; g < groups.Count; g++)
        {
            foreach (var (sku, name, unit, cost, price) in DemoProducts[g].Items)
                unitOfWork.Repository<Product>().Add(new Product(sku, name, unit, groups[g].Id, cost, price));
        }
    }

    /// <summary>Phiếu nhập tồn đầu kỳ cho kho A, B + ngưỡng tối thiểu (vài mã cố ý dưới ngưỡng để demo cảnh báo).</summary>
    private async Task<bool> EnsureOpeningStockAsync(CancellationToken ct)
    {
        if (await unitOfWork.Repository<StockDocument>().AnyAsync(ct)) return false;

        var products = await unitOfWork.Repository<Product>().OrderBy(p => p.Sku).ToListAsync(ct);
        var warehouses = await unitOfWork.Repository<Warehouse>().OrderBy(w => w.Code).Take(2).ToListAsync(ct);
        var supplier = await unitOfWork.Repository<Supplier>().OrderBy(s => s.Id).FirstAsync(ct);

        for (var w = 0; w < warehouses.Count; w++)
        {
            var receipt = StockDocument.NewReceipt(await numbers.NextAsync(DocumentType.GoodsReceipt, ct),
                warehouses[w].Id, supplier.Id, "Tồn đầu kỳ (dữ liệu demo)");
            for (var i = 0; i < products.Count; i++)
            {
                var quantity = (i * 37 + w * 53) % 400 + (i % 5 == 1 ? 5 : 40);
                receipt.AddLine(products[i].Id, quantity, products[i].Cost);
            }
            unitOfWork.Repository<StockDocument>().Add(receipt);
            await poster.PostAsync(receipt, ct);
        }

        foreach (var level in unitOfWork.Repository<StockLevel>().Local)
            level.SetMinThreshold(level.ProductId % 5 == 2 ? 60 : 30);
        return true;
    }
}
