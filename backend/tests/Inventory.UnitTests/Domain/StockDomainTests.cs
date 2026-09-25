using Inventory.Domain.Entities.Stock;
using Inventory.Domain.Enums;
using Inventory.Domain.Exceptions;

namespace Inventory.UnitTests.Domain;

public class StockDomainTests
{
    [Fact]
    public void Stock_level_never_goes_negative()
    {
        var level = new StockLevel(1, 1);
        level.Adjust(5);

        var ex = Assert.Throws<InsufficientStockException>(() => level.Adjust(-6, "P001"));

        Assert.Equal(5, level.Quantity);
        var shortage = Assert.Single(ex.Shortages);
        Assert.Equal((5m, 6m), (shortage.Available, shortage.Requested));
        Assert.Contains("P001", ex.Message);
    }

    [Fact]
    public void Threshold_marks_level_as_low_only_when_set()
    {
        var level = new StockLevel(1, 1);
        level.Adjust(10);
        Assert.False(level.IsBelowThreshold);

        level.SetMinThreshold(20);
        Assert.True(level.IsBelowThreshold);
        Assert.Throws<DomainException>(() => level.SetMinThreshold(-1));
    }

    [Fact]
    public void Document_rejects_duplicate_product_and_invalid_quantity()
    {
        var issue = StockDocument.NewIssue("PX-1", 1, IssueReason.Sale, null);
        issue.AddLine(1, 2);

        Assert.Throws<DomainException>(() => issue.AddLine(1, 3));
        Assert.Throws<DomainException>(() => issue.AddLine(2, 0));

        var take = StockDocument.NewStockTake("KK-1", 1, null);
        take.AddLine(1, 0); // kiểm kê được phép đếm = 0
        Assert.Single(take.Lines);
    }

    [Fact]
    public void Posted_document_is_final()
    {
        var doc = StockDocument.NewReceipt("PN-1", 1, 1, null);
        Assert.Throws<DomainException>(() => doc.MarkPosted(DateTime.UtcNow, null, null)); // chưa có dòng

        doc.AddLine(1, 5, 1000);
        doc.MarkPosted(DateTime.UtcNow, Guid.NewGuid(), "Manager");

        Assert.Equal(DocumentStatus.Posted, doc.Status);
        Assert.Throws<DomainException>(() => doc.MarkPosted(DateTime.UtcNow, null, null));
        Assert.Throws<DomainException>(() => doc.Cancel());
        Assert.Throws<DomainException>(() => doc.AddLine(2, 1));
    }

    [Fact]
    public void Transfer_requires_different_warehouses() =>
        Assert.Throws<DomainException>(() => StockDocument.NewTransfer("CK-1", 1, 1, null));

    [Fact]
    public void Unit_cost_is_kept_only_on_receipts()
    {
        var issue = StockDocument.NewIssue("PX-1", 1, IssueReason.Sale, null);
        issue.AddLine(1, 2, unitCost: 500);
        Assert.Null(issue.Lines.Single().UnitCost);
    }

    [Fact]
    public void Sequence_formats_code_per_type_and_year()
    {
        var sequence = new DocumentSequence(DocumentType.GoodsIssue, 2026);
        Assert.Equal("PX-2026-00001", sequence.Next());
        Assert.Equal("PX-2026-00002", sequence.Next());
    }
}
