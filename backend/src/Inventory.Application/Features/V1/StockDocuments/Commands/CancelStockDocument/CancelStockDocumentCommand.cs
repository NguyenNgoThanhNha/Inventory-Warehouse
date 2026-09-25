using System.Text.Json.Serialization;
using FluentValidation;
using Inventory.Application.Features.V1.StockDocuments.DTOs;
using Inventory.Application.Features.V1.StockDocuments.Services;
using Inventory.Domain.Entities.Stock;

namespace Inventory.Application.Features.V1.StockDocuments.Commands.CancelStockDocument;

/// <summary>
/// Hủy phiếu nháp (không đụng tồn kho). Phiếu đã ghi sổ không hủy được — muốn đảo thì lập phiếu ngược lại.
/// Không thử lại khi xung đột: phiếu vừa bị người khác duyệt thì người hủy phải thấy lỗi 409.
/// </summary>
public sealed record CancelStockDocumentCommand(string RowVersion) : IRequest<StockDocumentDto>
{
    [JsonIgnore]
    public int Id { get; init; }

    [JsonIgnore]
    public DocumentType Type { get; init; }
}

public sealed class CancelStockDocumentCommandValidator : AbstractValidator<CancelStockDocumentCommand>
{
    public CancelStockDocumentCommandValidator() =>
        RuleFor(x => x.RowVersion).Must(StockDocumentRules.IsBase64).WithMessage("rowVersion không hợp lệ.");
}

public sealed class CancelStockDocumentCommandHandler(IUnitOfWork<InventoryDbContext> unitOfWork, IStockDocumentReader reader)
    : IRequestHandler<CancelStockDocumentCommand, StockDocumentDto>
{
    public async Task<StockDocumentDto> Handle(CancelStockDocumentCommand request, CancellationToken ct)
    {
        var document = await unitOfWork.Repository<StockDocument>()
                           .FirstOrDefaultAsync(d => d.Id == request.Id && d.Type == request.Type, ct)
                       ?? throw new NotFoundException("StockDocument", request.Id);

        StockDocumentRules.EnsureNotStale(document, request.RowVersion);
        document.Cancel();

        await unitOfWork.SaveChangesAsync(ct);
        return await reader.GetAsync(document.Id, request.Type, ct);
    }
}
