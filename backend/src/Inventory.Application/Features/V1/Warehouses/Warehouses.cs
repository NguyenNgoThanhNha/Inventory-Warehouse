namespace Inventory.Application.Features.V1.Warehouses.DTOs
{
    public sealed record WarehouseDto(int Id, string Code, string Name, string? Address, bool IsActive);
}

namespace Inventory.Application.Features.V1.Warehouses.Queries
{
    using Inventory.Application.Features.V1.Warehouses.DTOs;
    using Inventory.Domain.Entities.Catalog;

    /// <summary>Số kho ít (vài chục) nên trả hết, không phân trang.</summary>
    public sealed record GetWarehousesQuery(bool IncludeInactive = false) : IRequest<IReadOnlyList<WarehouseDto>>;

    public sealed class GetWarehousesQueryHandler(IUnitOfWork<InventoryDbContext> unitOfWork)
        : IRequestHandler<GetWarehousesQuery, IReadOnlyList<WarehouseDto>>
    {
        public async Task<IReadOnlyList<WarehouseDto>> Handle(GetWarehousesQuery request, CancellationToken ct)
        {
            var query = unitOfWork.Repository<Warehouse>().AsNoTracking();
            if (!request.IncludeInactive) query = query.Where(w => w.IsActive);
            return await query.OrderBy(w => w.Code)
                .Select(w => new WarehouseDto(w.Id, w.Code, w.Name, w.Address, w.IsActive))
                .ToListAsync(ct);
        }
    }
}

namespace Inventory.Application.Features.V1.Warehouses.Commands
{
    using System.Text.Json.Serialization;
    using FluentValidation;
    using Inventory.Application.Features.V1.Warehouses.DTOs;
    using Inventory.Domain.Entities.Catalog;
    using Inventory.Domain.Entities.Stock;

    /// <summary>
    /// Tạo mới (Id = null) hoặc cập nhật kho. Mã kho chỉ đặt lúc tạo.
    /// Không có API xóa kho: kho đã có tồn/chứng từ chỉ được ngừng hoạt động (IsActive = false).
    /// </summary>
    public sealed record SaveWarehouseCommand(string Code, string Name, string? Address, bool IsActive = true) : IRequest<WarehouseDto>
    {
        [JsonIgnore]
        public int? Id { get; init; }
    }

    public sealed class SaveWarehouseCommandValidator : AbstractValidator<SaveWarehouseCommand>
    {
        public SaveWarehouseCommandValidator(IUnitOfWork<InventoryDbContext> unitOfWork)
        {
            RuleFor(x => x.Code).NotEmpty().MaximumLength(20).Matches("^[A-Za-z0-9_-]+$")
                .MustAsync(async (code, ct) =>
                {
                    var normalized = Warehouse.NormalizeCode(code);
                    return !await unitOfWork.Repository<Warehouse>().AnyAsync(w => w.Code == normalized, ct);
                })
                .When(x => x.Id is null)
                .WithMessage("Mã kho đã tồn tại.");
            RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Address).MaximumLength(300);
        }
    }

    public sealed class SaveWarehouseCommandHandler(IUnitOfWork<InventoryDbContext> unitOfWork)
        : IRequestHandler<SaveWarehouseCommand, WarehouseDto>
    {
        public async Task<WarehouseDto> Handle(SaveWarehouseCommand request, CancellationToken ct)
        {
            Warehouse warehouse;
            if (request.Id is { } id)
            {
                warehouse = await unitOfWork.Repository<Warehouse>().FirstOrDefaultAsync(w => w.Id == id, ct)
                            ?? throw new NotFoundException("Warehouse", id);

                if (warehouse.IsActive && !request.IsActive
                    && await unitOfWork.Repository<StockLevel>().AnyAsync(s => s.WarehouseId == id && s.Quantity > 0, ct))
                    throw new ConflictException("Kho còn hàng, không thể ngừng hoạt động. Hãy chuyển hết hàng sang kho khác trước.");

                warehouse.Update(request.Name, request.Address, request.IsActive);
            }
            else
            {
                warehouse = new Warehouse(request.Code, request.Name, request.Address);
                unitOfWork.Repository<Warehouse>().Add(warehouse);
            }

            await unitOfWork.SaveChangesAsync(ct);
            return new WarehouseDto(warehouse.Id, warehouse.Code, warehouse.Name, warehouse.Address, warehouse.IsActive);
        }
    }
}
