using System.Text.Json.Serialization;
using FluentValidation;
using Inventory.Application.Common.Behaviors;
using Inventory.Application.Features.V1.StockDocuments.DTOs;
using Inventory.Application.Features.V1.StockDocuments.Services;
using Inventory.Domain.Entities.Stock;

namespace Inventory.Application.Features.V1.StockDocuments.Commands.CreateGoodsIssue;

/// <summary>
/// Lập phiếu xuất kho. <see cref="Post"/> = true → ghi sổ luôn (giảm tồn), cần quyền GOODS_ISSUE:U.
/// Thiếu hàng → 409 liệt kê các dòng thiếu. Hai phiếu tranh cùng một dòng tồn → phiếu sau được thử lại
/// với tồn mới nhất (ConflictRetryBehavior), không bao giờ làm tồn âm.
/// </summary>
public sealed record CreateGoodsIssueCommand(
    int WarehouseId,
    IssueReason Reason,
    string? Note,
    IReadOnlyList<DocumentLineInput> Lines,
    bool Post = false) : IRequest<StockDocumentDto>, IRetryOnConflict
{
    [JsonIgnore]
    public string? IdempotencyKey { get; init; }
}

public sealed class CreateGoodsIssueCommandValidator : AbstractValidator<CreateGoodsIssueCommand>
{
    public CreateGoodsIssueCommandValidator(IUnitOfWork<InventoryDbContext> unitOfWork)
    {
        RuleFor(x => x.WarehouseId).ActiveWarehouse(unitOfWork);
        RuleFor(x => x.Reason).IsInEnum();
        RuleFor(x => x.Note).MaximumLength(500);
        RuleFor(x => x.Lines).ValidLines(unitOfWork);
        RuleFor(x => x.IdempotencyKey).IdempotencyKey();
    }
}

public sealed class CreateGoodsIssueCommandHandler(
    IUnitOfWork<InventoryDbContext> unitOfWork, IStockDocumentWriter writer, IStockDocumentReader reader)
    : IRequestHandler<CreateGoodsIssueCommand, StockDocumentDto>
{
    public async Task<StockDocumentDto> Handle(CreateGoodsIssueCommand request, CancellationToken ct)
    {
        if (await writer.FindByIdempotencyKeyAsync(request.IdempotencyKey, nameof(CreateGoodsIssueCommand), ct) is { } existingId)
            return await reader.GetAsync(existingId, DocumentType.GoodsIssue, ct);

        var document = await writer.CreateAsync(DocumentType.GoodsIssue,
            code => StockDocument.NewIssue(code, request.WarehouseId, request.Reason, request.Note),
            request.Lines, request.Post, request.IdempotencyKey, nameof(CreateGoodsIssueCommand), ct);

        await unitOfWork.SaveChangesAsync(ct);
        return await reader.GetAsync(document.Id, DocumentType.GoodsIssue, ct);
    }
}
