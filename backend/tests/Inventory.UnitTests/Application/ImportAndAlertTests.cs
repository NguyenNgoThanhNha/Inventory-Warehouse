using Inventory.Application.Common.Caching;
using Inventory.Application.Common.Exceptions;
using Inventory.Application.Common.Interfaces;
using Inventory.Application.Features.V1.Imports.Commands.ImportProducts;
using Inventory.Application.Features.V1.StockAlerts.Commands.ScanLowStock;
using Inventory.Domain.Constants;
using Inventory.Domain.Entities.Catalog;
using Inventory.Domain.Entities.Stock;
using Inventory.Infrastructure.Caching;
using Inventory.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Inventory.UnitTests.Application;

public sealed class ImportAndAlertTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly ClosedXmlSpreadsheetService _xlsx = new();

    private static readonly string[] ProductHeaders = ["SKU", "Tên sản phẩm", "Đơn vị", "Nhóm hàng", "Giá vốn", "Giá bán", "Đang kinh doanh"];

    private MemoryStream Workbook(string[] headers, params object?[][] rows)
    {
        var bytes = _xlsx.Write([new SheetData("Data", headers.Select(h => new SheetColumn(h)).ToList(), rows)]);
        return new MemoryStream(bytes);
    }

    private ImportProductsCommandHandler ImportHandler() => new(_db.UnitOfWork, _xlsx,
        new CatalogCache(new DistributedCacheService(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
            NullLogger<DistributedCacheService>.Instance)),
        NullLogger<ImportProductsCommandHandler>.Instance);

    [Fact]
    public async Task Import_upserts_by_sku_creates_missing_groups_and_reports_bad_rows()
    {
        var group = new ProductGroup("Ốc vít");
        _db.Context.ProductGroups.Add(group);
        _db.Context.SaveChanges();
        _db.Context.Products.Add(new Product("P001", "Tên cũ", "cái", group.Id, 1, 2));
        _db.Context.SaveChanges();

        using var file = Workbook(ProductHeaders,
            ["p001", "Ốc vít M6", "cái", "ốc vít", 500m, 800m, "có"],        // row 2: update (SKU chuẩn hóa, nhóm không phân biệt hoa thường)
            ["P100", "Bản lề", "cái", "Phụ kiện cửa", "35.000", 52000m, null], // row 3: new + new group; "35.000" kiểu VN
            ["", "Thiếu SKU", "cái", "Ốc vít", 1m, 1m, null],                // row 4: error
            ["P101", "Giá âm", "cái", "Ốc vít", -5m, 1m, null],               // row 5: error
            ["P100", "Trùng", "cái", "Ốc vít", 1m, 1m, null],                 // row 6: duplicate SKU
            ["P102", "Cờ sai", "cái", "Ốc vít", 1m, 1m, "maybe"]);            // row 7: bad bool

        var result = await ImportHandler().Handle(new ImportProductsCommand(file, "p.xlsx", file.Length, DryRun: false), default);

        Assert.Equal((6, 2, 1, 1), (result.TotalRows, result.ValidRows, result.Created, result.Updated));
        Assert.Equal(["Phụ kiện cửa"], result.GroupsCreated);
        Assert.Equal([4, 5, 6, 7], result.Errors.Select(e => e.Row));
        Assert.Contains(result.Errors, e => e.Row == 6 && e.Message.Contains("dòng 3"));

        var products = _db.Context.Products.AsNoTracking().Include(p => p.Group).OrderBy(p => p.Sku).ToList();
        Assert.Equal(["P001", "P100"], products.Select(p => p.Sku));
        Assert.Equal(("Ốc vít M6", 500m), (products[0].Name, products[0].Cost));
        Assert.Equal((35000m, "Phụ kiện cửa"), (products[1].Cost, products[1].Group.Name));
    }

    [Fact]
    public async Task Dry_run_validates_without_writing()
    {
        using var file = Workbook(ProductHeaders, ["P200", "Mới", "cái", "Nhóm mới", 1m, 2m, null]);

        var result = await ImportHandler().Handle(new ImportProductsCommand(file, "p.xlsx", file.Length, DryRun: true), default);

        Assert.Equal((1, 1), (result.Created, result.ValidRows));
        Assert.Empty(_db.Context.Products);
        Assert.Empty(_db.Context.ProductGroups);
    }

    [Fact]
    public async Task Missing_required_columns_is_a_file_level_error()
    {
        using var file = Workbook(["SKU", "Tên sản phẩm"], ["P1", "X"]);
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            ImportHandler().Handle(new ImportProductsCommand(file, "p.xlsx", file.Length, false), default));
        Assert.Contains("Đơn vị", ex.Errors["file"][0]);
    }

    [Fact]
    public void Not_an_xlsx_is_rejected_cleanly()
    {
        using var junk = new MemoryStream("hello"u8.ToArray());
        var ex = Assert.Throws<ValidationException>(() => _xlsx.Read(junk, 10));
        Assert.True(ex.Errors.ContainsKey("file"));
    }

    [Theory]
    [InlineData("  TÊN  SẢN PHẨM ", "ten san pham")]
    [InlineData("Đơn vị", "don vi")]
    [InlineData("Giá vốn", "gia von")]
    public void Headers_match_ignoring_case_spaces_and_diacritics(string header, string expected) =>
        Assert.Equal(expected, SheetHeader.Normalize(header));

    [Theory]
    [InlineData("1234.5", 1234.5)]
    [InlineData("1.234,5", 1234.5)]
    [InlineData(" 35.000 ", 35000)]
    public void Numbers_accept_invariant_and_vietnamese_formats(string text, double expected)
    {
        Assert.True(SheetValue.TryDecimal(text, out var value));
        Assert.Equal((decimal)expected, value);
    }

    [Fact]
    public void Numeric_cells_are_taken_as_is_not_reparsed_as_vietnamese_text()
    {
        // Ô số 1.234 (một phẩy hai ba bốn) — nếu đổi sang chuỗi "1.234" rồi đọc kiểu VN sẽ thành 1234.
        Assert.True(SheetValue.TryDecimal(1.234d, out var qty));
        Assert.Equal(1.234m, qty);
        Assert.True(SheetValue.TryDecimal("1,5", out var comma));
        Assert.Equal(1.5m, comma);
        Assert.Equal(true, SheetValue.TryBool(true));
        Assert.Equal(false, SheetValue.TryBool("không"));
    }

    [Fact]
    public async Task Low_stock_scan_opens_once_resolves_on_recovery_and_notifies_managers_per_warehouse()
    {
        var act = _db.SeedActivities();
        var manager = _db.AddUser("m@x.vn", true, _db.AddRole("Manager", null, (act[ConstActivity.Warehouse], "RU")));
        var staff = _db.AddUser("s@x.vn", true, _db.AddRole("Staff", null, (act[ConstActivity.Warehouse], "R")));
        var group = new ProductGroup("G");
        _db.Context.ProductGroups.Add(group);
        _db.Context.SaveChanges();
        var p1 = new Product("P1", "A", "cái", group.Id, 1, 1);
        var p2 = new Product("P2", "B", "cái", group.Id, 1, 1);
        var wh = new Warehouse("KA", "Kho A", null);
        _db.Context.AddRange(p1, p2, wh);
        _db.Context.SaveChanges();
        var l1 = new StockLevel(p1.Id, wh.Id);
        l1.Adjust(5);
        l1.SetMinThreshold(10); // dưới ngưỡng
        var l2 = new StockLevel(p2.Id, wh.Id);
        l2.Adjust(50);
        l2.SetMinThreshold(10); // đủ
        _db.Context.StockLevels.AddRange(l1, l2);
        _db.Context.SaveChanges();
        var handler = new ScanLowStockCommandHandler(_db.UnitOfWork, new FakeTimeProvider(DateTimeOffset.UtcNow));

        var first = await handler.Handle(new ScanLowStockCommand(), default);
        var again = await handler.Handle(new ScanLowStockCommand(), default);

        Assert.Equal((1, 0, 1), (first.Opened, first.Resolved, first.Notified));
        Assert.Equal((0, 0, 0), (again.Opened, again.Resolved, again.Notified)); // idempotent
        var note = Assert.Single(_db.Context.SysNotifications);
        Assert.Equal(manager.Id, note.UserId);
        Assert.NotEqual(staff.Id, note.UserId);
        Assert.Equal($"/stock?belowThreshold=true&warehouseId={wh.Id}", note.Link);

        l1.Adjust(20); // hồi hàng
        _db.Context.SaveChanges();
        var recovered = await handler.Handle(new ScanLowStockCommand(), default);

        Assert.Equal((0, 1, 0), (recovered.Opened, recovered.Resolved, recovered.StillOpen));
        Assert.True(_db.Context.StockAlerts.Single().IsResolved);
    }

    public void Dispose() => _db.Dispose();
}
