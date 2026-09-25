using System.Text.Json.Serialization;
using FluentValidation;
using Inventory.Application.Common.Behaviors;
using Inventory.Application.Features.V1.StockDocuments.DTOs;
using Inventory.Application.Features.V1.StockDocuments.Services;
using Inventory.Domain.Entities.Catalog;
using Inventory.Domain.Entities.Stock;

namespace Inventory.Application.Features.V1.StockDocuments.Commands.CreateGoodsReceipt;

/// <summary>Lập phiếu nhập kho. <see cref="Post"/> = true → ghi sổ luôn (tăng tồn), cần quyền GOODS_RECEIPT:U.</summary>
public sealed record CreateGoodsReceiptCommand(
    int WarehouseId,
    int SupplierId,
    string? Note,
    IReadOnlyList<DocumentLineInput> Lines,
    bool Post = false) : IRequest<StockDocumentDto>, IRetryOnConflict
{
    [JsonIgnore]
    public string? IdempotencyKey { get; init; }
}

public sealed class CreateGoodsReceiptCommandValidator : AbstractValidator<CreateGoodsReceiptCommand>
{
    public CreateGoodsReceiptCommandValidator(IUnitOfWork<InventoryDbContext> unitOfWork)
    {
        RuleFor(x => x.WarehouseId).ActiveWarehouse(unitOfWork);
        RuleFor(x => x.SupplierId)
            .MustAsync((id, ct) => unitOfWork.Repository<Supplier>().AnyAsync(s => s.Id == id, ct))
            .WithMessage("Nhà cung cấp không tồn tại.");
        RuleFor(x => x.Note).MaximumLength(500);
        RuleFor(x => x.Lines).ValidLines(unitOfWork);
        RuleFor(x => x.IdempotencyKey).IdempotencyKey();
    }
}

public sealed class CreateGoodsReceiptCommandHandler(
    IUnitOfWork<InventoryDbContext> unitOfWork, IStockDocumentWriter writer, IStockDocumentReader reader)
    : IRequestHandler<CreateGoodsReceiptCommand, StockDocumentDto>
{
    public async Task<StockDocumentDto> Handle(CreateGoodsReceiptCommand request, CancellationToken ct)
    {
        if (await writer.FindByIdempotencyKeyAsync(request.IdempotencyKey, nameof(CreateGoodsReceiptCommand), ct) is { } existingId)
            return await reader.GetAsync(existingId, DocumentType.GoodsReceipt, ct);

        var document = await writer.CreateAsync(DocumentType.GoodsReceipt,
            code => StockDocument.NewReceipt(code, request.WarehouseId, request.SupplierId, request.Note),
            request.Lines, request.Post, request.IdempotencyKey, nameof(CreateGoodsReceiptCommand), ct);

        await unitOfWork.SaveChangesAsync(ct);
        return await reader.GetAsync(document.Id, DocumentType.GoodsReceipt, ct);
    }
}
