using System.Text.Json.Serialization;
using FluentValidation;
using Inventory.Application.Common.Behaviors;
using Inventory.Application.Features.V1.StockDocuments.DTOs;
using Inventory.Application.Features.V1.StockDocuments.Services;
using Inventory.Domain.Entities.Stock;

namespace Inventory.Application.Features.V1.StockDocuments.Commands.CreateTransfer;

/// <summary>
/// Lập phiếu chuyển kho. Ghi sổ = trừ kho xuất + cộng kho nhận trong MỘT lần SaveChanges (một transaction):
/// thiếu hàng ở kho xuất hoặc xung đột bất kỳ → không kho nào bị đổi.
/// </summary>
public sealed record CreateTransferCommand(
    int FromWarehouseId,
    int ToWarehouseId,
    string? Note,
    IReadOnlyList<DocumentLineInput> Lines,
    bool Post = false) : IRequest<StockDocumentDto>, IRetryOnConflict
{
    [JsonIgnore]
    public string? IdempotencyKey { get; init; }
}

public sealed class CreateTransferCommandValidator : AbstractValidator<CreateTransferCommand>
{
    public CreateTransferCommandValidator(IUnitOfWork<InventoryDbContext> unitOfWork)
    {
        RuleFor(x => x.FromWarehouseId).ActiveWarehouse(unitOfWork);
        RuleFor(x => x.ToWarehouseId).ActiveWarehouse(unitOfWork)
            .NotEqual(x => x.FromWarehouseId).WithMessage("Kho nhận phải khác kho xuất.");
        RuleFor(x => x.Note).MaximumLength(500);
        RuleFor(x => x.Lines).ValidLines(unitOfWork);
        RuleFor(x => x.IdempotencyKey).IdempotencyKey();
    }
}

public sealed class CreateTransferCommandHandler(
    IUnitOfWork<InventoryDbContext> unitOfWork, IStockDocumentWriter writer, IStockDocumentReader reader)
    : IRequestHandler<CreateTransferCommand, StockDocumentDto>
{
    public async Task<StockDocumentDto> Handle(CreateTransferCommand request, CancellationToken ct)
    {
        if (await writer.FindByIdempotencyKeyAsync(request.IdempotencyKey, nameof(CreateTransferCommand), ct) is { } existingId)
            return await reader.GetAsync(existingId, DocumentType.Transfer, ct);

        var document = await writer.CreateAsync(DocumentType.Transfer,
            code => StockDocument.NewTransfer(code, request.FromWarehouseId, request.ToWarehouseId, request.Note),
            request.Lines, request.Post, request.IdempotencyKey, nameof(CreateTransferCommand), ct);

        await unitOfWork.SaveChangesAsync(ct);
        return await reader.GetAsync(document.Id, DocumentType.Transfer, ct);
    }
}
