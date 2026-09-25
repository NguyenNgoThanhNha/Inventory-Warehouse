using Inventory.Application.Common.Exceptions;
using Inventory.Application.Common.Interfaces;
using Inventory.Application.Features.V1.Stock.Services;
using Inventory.Application.Features.V1.StockDocuments.Commands.CreateGoodsIssue;
using Inventory.Application.Features.V1.StockDocuments.DTOs;
using Inventory.Application.Features.V1.StockDocuments.Services;
using Inventory.Domain.Entities.Catalog;
using Inventory.Domain.Entities.Stock;
using Inventory.Domain.Enums;
using Inventory.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace Inventory.UnitTests.Application;

/// <summary>Rule tồn kho: đi qua StockLedger, không âm, luôn có StockMovement, chuyển kho / kiểm kê đúng.</summary>
public sealed class StockLedgerTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 9, 25, 8, 0, 0, TimeSpan.Zero));
    private readonly ICurrentUser _user = Substitute.For<ICurrentUser>();
    private readonly int _p1, _p2, _whA, _whB, _supplier;

    public StockLedgerTests()
    {
        var group = new ProductGroup("Ốc vít");
        _db.Context.ProductGroups.Add(group);
        _db.Context.SaveChanges();

        var p1 = new Product("P001", "Ốc vít M6", "cái", group.Id, 500, 800);
        var p2 = new Product("P002", "Bản lề", "cái", group.Id, 35_000, 52_000);
        var whA = new Warehouse("KHO-A", "Kho A", null);
        var whB = new Warehouse("KHO-B", "Kho B", null);
        var supplier = new Supplier("NCC", null, null, null);
        _db.Context.AddRange(p1, p2, whA, whB, supplier);
        _db.Context.SaveChanges();
        (_p1, _p2, _whA, _whB, _supplier) = (p1.Id, p2.Id, whA.Id, whB.Id, supplier.Id);

        _user.UserId.Returns(Guid.NewGuid());
        _user.UserName.Returns("Tester");
        _user.HasPermissionAsync(Arg.Any<string>(), Arg.Any<ActivityType>(), Arg.Any<CancellationToken>()).Returns(true);
    }

    private StockLedger Ledger => new(_db.UnitOfWork, _user, _clock);
    private StockDocumentPoster Poster => new(Ledger, _user, _clock);

    private StockDocumentWriter Writer =>
        new(_db.UnitOfWork, _user, new DocumentNumberGenerator(_db.UnitOfWork, _clock), Poster, _clock);

    private async Task<StockDocument> PostAsync(StockDocument document)
    {
        _db.Context.StockDocuments.Add(document);
        await Poster.PostAsync(document, default);
        await _db.UnitOfWork.SaveChangesAsync();
        return document;
    }

    private async Task ReceiveAsync(int warehouseId, params (int ProductId, decimal Qty)[] lines)
    {
        var doc = StockDocument.NewReceipt($"PN-{Guid.NewGuid():N}"[..20], warehouseId, _supplier, null);
        foreach (var (productId, qty) in lines) doc.AddLine(productId, qty, 1000);
        await PostAsync(doc);
    }

    private decimal Qty(int productId, int warehouseId) =>
        _db.Context.StockLevels.AsNoTracking().SingleOrDefault(s => s.ProductId == productId && s.WarehouseId == warehouseId)?.Quantity ?? 0;

    [Fact]
    public async Task Receipt_increases_stock_and_writes_movement_with_balance()
    {
        await ReceiveAsync(_whA, (_p1, 100));
        await ReceiveAsync(_whA, (_p1, 20));

        Assert.Equal(120, Qty(_p1, _whA));
        var movements = _db.Context.StockMovements.OrderBy(m => m.Id).ToList();
        Assert.Equal([100m, 120m], movements.Select(m => m.BalanceAfter));
        Assert.All(movements, m => Assert.Equal(MovementType.In, m.Type));
        Assert.All(movements, m => Assert.Equal("Tester", m.CreatedName));
    }

    [Fact]
    public async Task Issue_beyond_stock_is_rejected_listing_every_short_line_and_changes_nothing()
    {
        await ReceiveAsync(_whA, (_p1, 10), (_p2, 3));
        var issue = StockDocument.NewIssue("PX-1", _whA, IssueReason.Sale, null);
        issue.AddLine(_p1, 11);
        issue.AddLine(_p2, 5);
        _db.Context.StockDocuments.Add(issue);

        var ex = await Assert.ThrowsAsync<InsufficientStockException>(() => Poster.PostAsync(issue, default));

        Assert.Equal(["P001", "P002"], ex.Shortages.Select(s => s.Sku));
        Assert.Equal(DocumentStatus.Draft, issue.Status);
        Assert.Equal(10, _db.Context.StockLevels.Local.Single(s => s.ProductId == _p1).Quantity); // chưa dòng nào bị trừ
        Assert.DoesNotContain(_db.Context.ChangeTracker.Entries<StockMovement>(), e => e.State == EntityState.Added);
    }

    [Fact]
    public async Task Issue_of_product_without_stock_row_is_rejected()
    {
        var issue = StockDocument.NewIssue("PX-1", _whB, IssueReason.Disposal, null);
        issue.AddLine(_p1, 1);
        _db.Context.StockDocuments.Add(issue);

        var ex = await Assert.ThrowsAsync<InsufficientStockException>(() => Poster.PostAsync(issue, default));
        Assert.Equal(0, ex.Shortages.Single().Available);
    }

    [Fact]
    public async Task Transfer_moves_quantity_between_warehouses_with_two_movements()
    {
        await ReceiveAsync(_whA, (_p1, 50));
        var transfer = StockDocument.NewTransfer("CK-1", _whA, _whB, null);
        transfer.AddLine(_p1, 30);

        await PostAsync(transfer);

        Assert.Equal((20m, 30m), (Qty(_p1, _whA), Qty(_p1, _whB)));
        var moves = _db.Context.StockMovements.Where(m => m.DocumentId == transfer.Id).OrderBy(m => m.Id).ToList();
        Assert.Equal([(MovementType.TransferOut, -30m), (MovementType.TransferIn, 30m)], moves.Select(m => (m.Type, m.Quantity)));
    }

    [Fact]
    public async Task Stock_take_records_system_quantity_and_adjusts_only_differences()
    {
        await ReceiveAsync(_whA, (_p1, 40), (_p2, 7));
        var take = StockDocument.NewStockTake("KK-1", _whA, null);
        take.AddLine(_p1, 37); // thiếu 3
        take.AddLine(_p2, 7);  // khớp

        await PostAsync(take);

        Assert.Equal((37m, 7m), (Qty(_p1, _whA), Qty(_p2, _whA)));
        Assert.Equal(40, take.Lines.Single(l => l.ProductId == _p1).SystemQuantity);
        var adjust = Assert.Single(_db.Context.StockMovements.Where(m => m.DocumentId == take.Id));
        Assert.Equal((MovementType.Adjust, -3m, 37m), (adjust.Type, adjust.Quantity, adjust.BalanceAfter));
    }

    [Fact]
    public async Task Every_stock_change_has_a_movement_and_ledger_sums_to_stock()
    {
        await ReceiveAsync(_whA, (_p1, 100));
        var issue = StockDocument.NewIssue("PX-1", _whA, IssueReason.Sale, null);
        issue.AddLine(_p1, 25);
        await PostAsync(issue);
        var transfer = StockDocument.NewTransfer("CK-1", _whA, _whB, null);
        transfer.AddLine(_p1, 15);
        await PostAsync(transfer);

        foreach (var level in _db.Context.StockLevels.AsNoTracking().ToList())
        {
            var sum = _db.Context.StockMovements.Where(m => m.ProductId == level.ProductId && m.WarehouseId == level.WarehouseId).Sum(m => m.Quantity);
            Assert.Equal(level.Quantity, sum);
        }
    }

    [Fact]
    public async Task Writer_requires_approve_permission_to_post_on_create()
    {
        _user.HasPermissionAsync(ConstActivityGoodsIssue, ActivityType.Update, Arg.Any<CancellationToken>()).Returns(false);

        await Assert.ThrowsAsync<ForbiddenException>(() => Writer.CreateAsync(DocumentType.GoodsIssue,
            code => StockDocument.NewIssue(code, _whA, IssueReason.Sale, null),
            [new DocumentLineInput(_p1, 1)], post: true, null, "test", default));

        var draft = await Writer.CreateAsync(DocumentType.GoodsIssue,
            code => StockDocument.NewIssue(code, _whA, IssueReason.Sale, null),
            [new DocumentLineInput(_p1, 1)], post: false, null, "test", default);
        Assert.Equal(DocumentStatus.Draft, draft.Status);
        Assert.Equal("PX-2026-00001", draft.Code);
    }

    [Fact]
    public async Task Same_idempotency_key_returns_the_first_document()
    {
        await ReceiveAsync(_whA, (_p1, 10));
        var handler = new CreateGoodsIssueCommandHandler(_db.UnitOfWork, Writer, new StockDocumentReader(_db.UnitOfWork));
        var command = new CreateGoodsIssueCommand(_whA, IssueReason.Sale, null, [new DocumentLineInput(_p1, 4)], Post: true)
        {
            IdempotencyKey = "k-1"
        };

        var first = await handler.Handle(command, default);
        var second = await handler.Handle(command, default);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(6, Qty(_p1, _whA)); // chỉ trừ một lần
        Assert.Equal(1, _db.Context.StockDocuments.Count(d => d.Type == DocumentType.GoodsIssue));
    }

    [Fact]
    public async Task Idempotency_key_reused_for_another_operation_is_a_conflict()
    {
        await ReceiveAsync(_whA, (_p1, 10));
        var handler = new CreateGoodsIssueCommandHandler(_db.UnitOfWork, Writer, new StockDocumentReader(_db.UnitOfWork));
        await handler.Handle(new CreateGoodsIssueCommand(_whA, IssueReason.Sale, null, [new DocumentLineInput(_p1, 1)]) { IdempotencyKey = "k-2" }, default);

        await Assert.ThrowsAsync<ConflictException>(() => Writer.FindByIdempotencyKeyAsync("k-2", "OtherCommand", default));
    }

    [Fact]
    public async Task Create_issue_validator_rejects_duplicate_and_inactive_products()
    {
        var inactive = _db.Context.Products.Single(p => p.Id == _p2);
        inactive.Update(inactive.Name, inactive.Unit, inactive.GroupId, inactive.Cost, inactive.Price, null, isActive: false);
        _db.Context.SaveChanges();
        var validator = new CreateGoodsIssueCommandValidator(_db.UnitOfWork);

        var duplicate = await validator.ValidateAsync(new CreateGoodsIssueCommand(_whA, IssueReason.Sale, null,
            [new DocumentLineInput(_p1, 1), new DocumentLineInput(_p1, 2)]));
        var inactiveResult = await validator.ValidateAsync(new CreateGoodsIssueCommand(_whA, IssueReason.Sale, null,
            [new DocumentLineInput(_p2, 1)]));
        var empty = await validator.ValidateAsync(new CreateGoodsIssueCommand(_whA, IssueReason.Sale, null, []));

        Assert.Contains(duplicate.Errors, e => e.ErrorMessage.Contains("lặp"));
        Assert.Contains(inactiveResult.Errors, e => e.ErrorMessage.Contains("ngừng kinh doanh"));
        Assert.False(empty.IsValid);
    }

    private const string ConstActivityGoodsIssue = Inventory.Domain.Constants.ConstActivity.GoodsIssue;

    public void Dispose() => _db.Dispose();
}
