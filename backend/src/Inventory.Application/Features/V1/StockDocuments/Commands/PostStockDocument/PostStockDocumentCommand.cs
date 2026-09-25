using System.Text.Json.Serialization;
using FluentValidation;
using Inventory.Application.Common.Behaviors;
using Inventory.Application.Features.V1.StockDocuments.DTOs;
using Inventory.Application.Features.V1.StockDocuments.Services;
using Inventory.Domain.Entities.Stock;

namespace Inventory.Application.Features.V1.StockDocuments.Commands.PostStockDocument;

/// <summary>
/// Duyệt/ghi sổ phiếu nháp → tồn kho thay đổi. Quyền U của loại phiếu được kiểm tra ở controller.
/// <see cref="RowVersion"/> là bản client đang xem: phiếu đã bị sửa/duyệt/hủy bởi người khác → 409.
/// </summary>
public sealed record PostStockDocumentCommand(string RowVersion) : IRequest<StockDocumentDto>, IRetryOnConflict
{
    [JsonIgnore]
    public int Id { get; init; }

    [JsonIgnore]
    public DocumentType Type { get; init; }
}

public sealed class PostStockDocumentCommandValidator : AbstractValidator<PostStockDocumentCommand>
{
    public PostStockDocumentCommandValidator() =>
        RuleFor(x => x.RowVersion).Must(StockDocumentRules.IsBase64).WithMessage("rowVersion không hợp lệ.");
}

public sealed class PostStockDocumentCommandHandler(
    IUnitOfWork<InventoryDbContext> unitOfWork, IStockDocumentPoster poster, IStockDocumentReader reader)
    : IRequestHandler<PostStockDocumentCommand, StockDocumentDto>
{
    public async Task<StockDocumentDto> Handle(PostStockDocumentCommand request, CancellationToken ct)
    {
        var document = await unitOfWork.Repository<StockDocument>()
                           .Include(d => d.Lines)
                           .FirstOrDefaultAsync(d => d.Id == request.Id && d.Type == request.Type, ct)
                       ?? throw new NotFoundException("StockDocument", request.Id);

        StockDocumentRules.EnsureNotStale(document, request.RowVersion);
        await poster.PostAsync(document, ct);

        await unitOfWork.SaveChangesAsync(ct);
        return await reader.GetAsync(document.Id, request.Type, ct);
    }
}
