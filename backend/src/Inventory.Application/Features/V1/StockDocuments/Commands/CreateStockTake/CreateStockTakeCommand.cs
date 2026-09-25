using System.Text.Json.Serialization;
using FluentValidation;
using Inventory.Application.Common.Behaviors;
using Inventory.Application.Features.V1.StockDocuments.DTOs;
using Inventory.Application.Features.V1.StockDocuments.Services;
using Inventory.Domain.Entities.Stock;

namespace Inventory.Application.Features.V1.StockDocuments.Commands.CreateStockTake;

/// <summary>
/// Lập phiếu kiểm kê: mỗi dòng là số đếm thực tế. Ghi sổ → so với tồn sổ sách tại thời điểm ghi sổ,
/// sinh movement Adjust cho phần chênh lệch và lưu lại tồn sổ sách vào dòng phiếu.
/// </summary>
public sealed record CreateStockTakeCommand(
    int WarehouseId,
    string? Note,
    IReadOnlyList<DocumentLineInput> Lines,
    bool Post = false) : IRequest<StockDocumentDto>, IRetryOnConflict
{
    [JsonIgnore]
    public string? IdempotencyKey { get; init; }
}

public sealed class CreateStockTakeCommandValidator : AbstractValidator<CreateStockTakeCommand>
{
    public CreateStockTakeCommandValidator(IUnitOfWork<InventoryDbContext> unitOfWork)
    {
        RuleFor(x => x.WarehouseId).ActiveWarehouse(unitOfWork);
        RuleFor(x => x.Note).MaximumLength(500);
        RuleFor(x => x.Lines).ValidLines(unitOfWork, allowZeroQuantity: true);
        RuleFor(x => x.IdempotencyKey).IdempotencyKey();
    }
}

public sealed class CreateStockTakeCommandHandler(
    IUnitOfWork<InventoryDbContext> unitOfWork, IStockDocumentWriter writer, IStockDocumentReader reader)
    : IRequestHandler<CreateStockTakeCommand, StockDocumentDto>
{
    public async Task<StockDocumentDto> Handle(CreateStockTakeCommand request, CancellationToken ct)
    {
        if (await writer.FindByIdempotencyKeyAsync(request.IdempotencyKey, nameof(CreateStockTakeCommand), ct) is { } existingId)
            return await reader.GetAsync(existingId, DocumentType.StockTake, ct);

        var document = await writer.CreateAsync(DocumentType.StockTake,
            code => StockDocument.NewStockTake(code, request.WarehouseId, request.Note),
            request.Lines.Select(l => l with { UnitCost = null }), request.Post, request.IdempotencyKey, nameof(CreateStockTakeCommand), ct);

        await unitOfWork.SaveChangesAsync(ct);
        return await reader.GetAsync(document.Id, DocumentType.StockTake, ct);
    }
}
