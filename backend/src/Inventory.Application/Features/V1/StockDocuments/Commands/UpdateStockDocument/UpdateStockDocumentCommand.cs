using System.Text.Json.Serialization;
using FluentValidation;
using Inventory.Application.Common.Behaviors;
using Inventory.Application.Features.V1.StockDocuments.DTOs;
using Inventory.Application.Features.V1.StockDocuments.Services;
using Inventory.Domain.Entities.Catalog;
using Inventory.Domain.Entities.Stock;

namespace Inventory.Application.Features.V1.StockDocuments.Commands.UpdateStockDocument;

/// <summary>
/// Sửa phiếu NHÁP (4 loại dùng chung): thông tin chung + thay toàn bộ dòng hàng. Không đổi tồn kho.
/// Trường nào không thuộc loại phiếu (vd SupplierId của phiếu xuất) bị bỏ qua.
/// Người sửa: người lập phiếu, hoặc người có quyền duyệt (U) loại phiếu đó.
/// <see cref="RowVersion"/> là bản client đang xem: phiếu đã bị sửa / duyệt / hủy bởi người khác → 409.
/// </summary>
public sealed record UpdateStockDocumentCommand(
    string RowVersion,
    int WarehouseId,
    int? ToWarehouseId,
    int? SupplierId,
    IssueReason? Reason,
    string? Note,
    IReadOnlyList<DocumentLineInput> Lines) : IRequest<StockDocumentDto>, IRetryOnConflict
{
    [JsonIgnore]
    public int Id { get; init; }

    [JsonIgnore]
    public DocumentType Type { get; init; }
}

public sealed class UpdateStockDocumentCommandValidator : AbstractValidator<UpdateStockDocumentCommand>
{
    public UpdateStockDocumentCommandValidator(IUnitOfWork<InventoryDbContext> unitOfWork)
    {
        RuleFor(x => x.RowVersion).Must(StockDocumentRules.IsBase64).WithMessage("rowVersion không hợp lệ.");
        RuleFor(x => x.WarehouseId).ActiveWarehouse(unitOfWork);
        RuleFor(x => x.Note).MaximumLength(500);

        When(x => x.Type == DocumentType.Transfer, () =>
            RuleFor(x => x.ToWarehouseId).NotNull().WithMessage("Chọn kho nhận.")
                .NotEqual(x => x.WarehouseId).WithMessage("Kho nhận phải khác kho xuất.")
                .MustAsync((id, ct) => unitOfWork.Repository<Warehouse>().AnyAsync(w => w.Id == id && w.IsActive, ct))
                .WithMessage("Kho không tồn tại hoặc đã ngừng hoạt động."));
        When(x => x.Type == DocumentType.GoodsReceipt, () =>
            RuleFor(x => x.SupplierId).NotNull().WithMessage("Chọn nhà cung cấp.")
                .MustAsync((id, ct) => unitOfWork.Repository<Supplier>().AnyAsync(s => s.Id == id, ct))
                .WithMessage("Nhà cung cấp không tồn tại."));
        When(x => x.Type == DocumentType.GoodsIssue, () =>
            RuleFor(x => x.Reason).NotNull().WithMessage("Chọn lý do xuất.").IsInEnum());

        RuleFor(x => x.Lines).ValidLines(unitOfWork).When(x => x.Type != DocumentType.StockTake);
        RuleFor(x => x.Lines).ValidLines(unitOfWork, allowZeroQuantity: true).When(x => x.Type == DocumentType.StockTake);
    }
}

public sealed class UpdateStockDocumentCommandHandler(
    IUnitOfWork<InventoryDbContext> unitOfWork, ICurrentUser currentUser, IStockDocumentReader reader)
    : IRequestHandler<UpdateStockDocumentCommand, StockDocumentDto>
{
    public async Task<StockDocumentDto> Handle(UpdateStockDocumentCommand request, CancellationToken ct)
    {
        var document = await unitOfWork.Repository<StockDocument>()
                           .Include(d => d.Lines)
                           .FirstOrDefaultAsync(d => d.Id == request.Id && d.Type == request.Type, ct)
                       ?? throw new NotFoundException("StockDocument", request.Id);

        // Phân quyền theo dữ liệu (RULES 5.4): chủ phiếu, hoặc người có quyền duyệt loại phiếu này.
        if (document.CreatedById != currentUser.UserId
            && !await currentUser.HasPermissionAsync(StockDocument.ActivityOf(request.Type), ActivityType.Update, ct))
            throw new ForbiddenException("Chỉ người lập phiếu hoặc người có quyền duyệt mới sửa được phiếu nháp này.");

        StockDocumentRules.EnsureNotStale(document, request.RowVersion);
        document.UpdateDraft(request.WarehouseId, request.ToWarehouseId, request.SupplierId, request.Reason, request.Note);
        foreach (var line in request.Lines)
            document.AddLine(line.ProductId, line.Quantity, request.Type == DocumentType.GoodsReceipt ? line.UnitCost : null, line.Note);

        await unitOfWork.SaveChangesAsync(ct);
        return await reader.GetAsync(document.Id, request.Type, ct);
    }
}
