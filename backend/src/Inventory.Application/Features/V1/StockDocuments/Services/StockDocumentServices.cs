using FluentValidation;
using Inventory.Application.Features.V1.Stock.Services;
using Inventory.Application.Features.V1.StockDocuments.DTOs;
using Inventory.Domain.Entities.Catalog;
using Inventory.Domain.Entities.Stock;
using Inventory.Persistence.Interceptors;

namespace Inventory.Application.Features.V1.StockDocuments.Services;

/// <summary>Cấp số chứng từ PN-2026-00001... Bộ đếm có RowVersion: hai phiếu lấy số cùng lúc thì một phiếu được thử lại.</summary>
public interface IDocumentNumberGenerator
{
    Task<string> NextAsync(DocumentType type, CancellationToken ct);
}

public sealed class DocumentNumberGenerator(IUnitOfWork<InventoryDbContext> unitOfWork, TimeProvider clock) : IDocumentNumberGenerator
{
    public async Task<string> NextAsync(DocumentType type, CancellationToken ct)
    {
        var year = clock.GetUtcNow().Year;
        var set = unitOfWork.Repository<DocumentSequence>();
        var sequence = set.Local.FirstOrDefault(s => s.Type == type && s.Year == year)
                       ?? await set.FirstOrDefaultAsync(s => s.Type == type && s.Year == year, ct);
        if (sequence is null)
        {
            sequence = new DocumentSequence(type, year);
            set.Add(sequence);
        }
        return sequence.Next();
    }
}

/// <summary>Ghi sổ một phiếu nháp: đổi phiếu thành các <see cref="StockChange"/> rồi giao cho <see cref="IStockLedger"/>.</summary>
public interface IStockDocumentPoster
{
    /// <summary>Phiếu phải được load kèm Lines. Không SaveChanges.</summary>
    Task PostAsync(StockDocument document, CancellationToken ct);
}

public sealed class StockDocumentPoster(IStockLedger ledger, IAuditUser auditUser, TimeProvider clock) : IStockDocumentPoster
{
    public async Task PostAsync(StockDocument document, CancellationToken ct)
    {
        var changes = document.Type switch
        {
            DocumentType.GoodsReceipt => document.Lines
                .Select(l => new StockChange(l.ProductId, document.WarehouseId, l.Quantity, MovementType.In)).ToList(),
            DocumentType.GoodsIssue => document.Lines
                .Select(l => new StockChange(l.ProductId, document.WarehouseId, -l.Quantity, MovementType.Out)).ToList(),
            // Trừ kho A + cộng kho B trong cùng một lần SaveChanges → nguyên tử: lỗi ở đâu thì cả hai cùng không xảy ra.
            DocumentType.Transfer => document.Lines
                .SelectMany(l => new[]
                {
                    new StockChange(l.ProductId, document.WarehouseId, -l.Quantity, MovementType.TransferOut),
                    new StockChange(l.ProductId, document.ToWarehouseId!.Value, l.Quantity, MovementType.TransferIn)
                }).ToList(),
            DocumentType.StockTake => await StockTakeChangesAsync(document, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(document), document.Type, null)
        };

        await ledger.ApplyAsync(document, changes, ct);
        document.MarkPosted(clock.GetUtcNow().UtcDateTime, auditUser.UserIdOrNull, auditUser.UserName);
    }

    /// <summary>Kiểm kê: chênh lệch = số đếm − tồn sổ sách lúc ghi sổ. Tồn sổ sách được chụp vào dòng phiếu.</summary>
    private async Task<List<StockChange>> StockTakeChangesAsync(StockDocument document, CancellationToken ct)
    {
        var levels = await ledger.LoadAsync(document.Lines.Select(l => (l.ProductId, document.WarehouseId)).ToList(), ct);
        var changes = new List<StockChange>();
        foreach (var line in document.Lines)
        {
            var system = levels.GetValueOrDefault((line.ProductId, document.WarehouseId))?.Quantity ?? 0;
            line.RecordSystemQuantity(system);
            if (line.Quantity != system)
                changes.Add(new StockChange(line.ProductId, document.WarehouseId, line.Quantity - system, MovementType.Adjust));
        }
        return changes;
    }
}

/// <summary>Luồng tạo phiếu dùng chung cho 4 loại: Idempotency-Key, số chứng từ, dòng hàng, ghi sổ ngay nếu được yêu cầu.</summary>
public interface IStockDocumentWriter
{
    /// <summary>Id phiếu đã tạo trước đó bằng cùng Idempotency-Key của user hiện tại; null nếu chưa có.</summary>
    Task<int?> FindByIdempotencyKeyAsync(string? idempotencyKey, string requestName, CancellationToken ct);

    /// <summary>Dựng phiếu (chưa SaveChanges). <paramref name="post"/> = true cần quyền U (duyệt) của loại phiếu.</summary>
    Task<StockDocument> CreateAsync(
        DocumentType type,
        Func<string, StockDocument> factory,
        IEnumerable<DocumentLineInput> lines,
        bool post,
        string? idempotencyKey,
        string requestName,
        CancellationToken ct);
}

public sealed class StockDocumentWriter(
    IUnitOfWork<InventoryDbContext> unitOfWork,
    ICurrentUser currentUser,
    IDocumentNumberGenerator numbers,
    IStockDocumentPoster poster,
    TimeProvider clock) : IStockDocumentWriter
{
    public async Task<int?> FindByIdempotencyKeyAsync(string? idempotencyKey, string requestName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)) return null;
        var userId = currentUser.UserId;
        var record = await unitOfWork.Repository<IdempotencyRecord>().AsNoTracking()
            .Where(r => r.UserId == userId && r.Key == idempotencyKey)
            .Select(r => new { r.DocumentId, r.RequestName })
            .FirstOrDefaultAsync(ct);
        if (record is null) return null;
        if (record.RequestName != requestName)
            throw new ConflictException($"{ConstHeader.IdempotencyKey} này đã được dùng cho một thao tác khác.");
        return record.DocumentId;
    }

    public async Task<StockDocument> CreateAsync(
        DocumentType type,
        Func<string, StockDocument> factory,
        IEnumerable<DocumentLineInput> lines,
        bool post,
        string? idempotencyKey,
        string requestName,
        CancellationToken ct)
    {
        if (post && !await currentUser.HasPermissionAsync(StockDocument.ActivityOf(type), ActivityType.Update, ct))
            throw new ForbiddenException("Bạn chỉ được lập phiếu nháp. Ghi sổ cần quyền duyệt phiếu.");

        var document = factory(await numbers.NextAsync(type, ct));
        foreach (var line in lines) document.AddLine(line.ProductId, line.Quantity, line.UnitCost, line.Note);
        unitOfWork.Repository<StockDocument>().Add(document);

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
            unitOfWork.Repository<IdempotencyRecord>().Add(new IdempotencyRecord(
                currentUser.UserId, idempotencyKey, requestName, document, clock.GetUtcNow().UtcDateTime));

        if (post) await poster.PostAsync(document, ct);
        return document;
    }
}

public interface IStockDocumentReader
{
    /// <summary><paramref name="type"/> khác loại thật của phiếu → 404 (không cho xem phiếu nhập qua route phiếu xuất).</summary>
    Task<StockDocumentDto> GetAsync(int id, DocumentType? type, CancellationToken ct);
}

public sealed class StockDocumentReader(IUnitOfWork<InventoryDbContext> unitOfWork) : IStockDocumentReader
{
    public async Task<StockDocumentDto> GetAsync(int id, DocumentType? type, CancellationToken ct)
    {
        var query = unitOfWork.Repository<StockDocument>().AsNoTracking().Where(d => d.Id == id);
        if (type is { } t) query = query.Where(d => d.Type == t);

        var header = await query.Select(d => new
            {
                d.Id, d.Code, d.Type, d.Status,
                Warehouse = new RefDto(d.Warehouse.Id, d.Warehouse.Name),
                ToWarehouse = d.ToWarehouse == null ? null : new RefDto(d.ToWarehouse.Id, d.ToWarehouse.Name),
                Supplier = d.Supplier == null ? null : new RefDto(d.Supplier.Id, d.Supplier.Name),
                d.Reason, d.Note, d.CreatedDate, d.CreatedName, d.PostedAt, d.PostedByName, d.RowVersion
            })
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException("StockDocument", id);

        var lines = await unitOfWork.Repository<StockDocumentLine>().AsNoTracking()
            .Where(l => l.DocumentId == id)
            .OrderBy(l => l.Id)
            .Select(l => new StockDocumentLineDto(
                l.Id, l.ProductId, l.Product.Sku, l.Product.Name, l.Product.Unit, l.Quantity, l.UnitCost, l.SystemQuantity,
                l.SystemQuantity == null ? null : l.Quantity - l.SystemQuantity, l.Note))
            .ToListAsync(ct);

        return new StockDocumentDto(header.Id, header.Code, header.Type, header.Status, header.Warehouse, header.ToWarehouse,
            header.Supplier, header.Reason, header.Note, header.CreatedDate, header.CreatedName, header.PostedAt,
            header.PostedByName, Convert.ToBase64String(header.RowVersion ?? []), lines);
    }
}

/// <summary>Rule dùng chung cho validator các lệnh tạo phiếu.</summary>
public static class StockDocumentRules
{
    public const int MaxLines = 500;
    public const decimal MaxQuantity = 1_000_000_000;
    public const int MaxIdempotencyKeyLength = 100;

    public static IRuleBuilderOptions<T, int> ActiveWarehouse<T>(this IRuleBuilder<T, int> rule, IUnitOfWork<InventoryDbContext> unitOfWork) =>
        rule.MustAsync((id, ct) => unitOfWork.Repository<Warehouse>().AnyAsync(w => w.Id == id && w.IsActive, ct))
            .WithMessage("Kho không tồn tại hoặc đã ngừng hoạt động.");

    public static IRuleBuilderOptions<T, string?> IdempotencyKey<T>(this IRuleBuilder<T, string?> rule) =>
        rule.MaximumLength(MaxIdempotencyKeyLength).Matches("^[A-Za-z0-9_.:-]*$")
            .WithMessage($"{ConstHeader.IdempotencyKey} chỉ gồm chữ, số và _ . : -");

    /// <summary>Không rỗng, không lặp sản phẩm, số lượng hợp lệ, mọi sản phẩm tồn tại và đang kinh doanh (1 query).</summary>
    public static IRuleBuilderOptions<T, IReadOnlyList<DocumentLineInput>> ValidLines<T>(
        this IRuleBuilder<T, IReadOnlyList<DocumentLineInput>> rule, IUnitOfWork<InventoryDbContext> unitOfWork, bool allowZeroQuantity = false) =>
        rule.NotEmpty().WithMessage("Phiếu phải có ít nhất một dòng hàng.")
            .Must(lines => lines.Count <= MaxLines).WithMessage($"Tối đa {MaxLines} dòng mỗi phiếu.")
            .Must(lines => lines.Select(l => l.ProductId).Distinct().Count() == lines.Count).WithMessage("Có sản phẩm bị lặp trong phiếu.")
            .Must(lines => lines.All(l => (allowZeroQuantity ? l.Quantity >= 0 : l.Quantity > 0) && l.Quantity <= MaxQuantity))
            .WithMessage(allowZeroQuantity ? "Số lượng đếm phải từ 0 trở lên." : "Số lượng phải lớn hơn 0.")
            .Must(lines => lines.All(l => l.UnitCost is null or >= 0)).WithMessage("Đơn giá không được âm.")
            .Must(lines => lines.All(l => (l.Note?.Length ?? 0) <= 200)).WithMessage("Ghi chú dòng tối đa 200 ký tự.")
            .MustAsync(async (lines, ct) =>
            {
                var ids = lines.Select(l => l.ProductId).Distinct().ToList();
                return await unitOfWork.Repository<Product>().CountAsync(p => ids.Contains(p.Id) && p.IsActive, ct) == ids.Count;
            })
            .WithMessage("Có sản phẩm không tồn tại hoặc đã ngừng kinh doanh.");

    /// <summary>So RowVersion client gửi lên với bản đang có trong DB (chuẩn BE §4.7).</summary>
    public static void EnsureNotStale(StockDocument document, string rowVersion)
    {
        if (document.RowVersion is not { Length: > 0 }) return; // provider không hỗ trợ rowversion (vd: InMemory)
        if (!document.RowVersion.AsSpan().SequenceEqual(Convert.FromBase64String(rowVersion)))
            throw new ConflictException(ConstMessage.ConcurrencyConflict); // không dùng DbUpdateConcurrencyException: lỗi này thử lại cũng vô ích
    }

    public static bool IsBase64(string? value) =>
        !string.IsNullOrEmpty(value) && Convert.TryFromBase64String(value, new byte[value.Length], out _);
}
