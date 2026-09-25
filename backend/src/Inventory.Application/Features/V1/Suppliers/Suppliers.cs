namespace Inventory.Application.Features.V1.Suppliers.DTOs
{
    public sealed record SupplierDto(int Id, string Name, string? Phone, string? Email, string? Address);
}

namespace Inventory.Application.Features.V1.Suppliers.Queries
{
    using Inventory.Application.Features.V1.Suppliers.DTOs;
    using Inventory.Domain.Entities.Catalog;

    public sealed record SearchSuppliersQuery : PagedQuery, IRequest<PagedResult<SupplierDto>>
    {
        public string? Search { get; init; }
    }

    public sealed class SearchSuppliersQueryHandler(IUnitOfWork<InventoryDbContext> unitOfWork)
        : IRequestHandler<SearchSuppliersQuery, PagedResult<SupplierDto>>
    {
        public async Task<PagedResult<SupplierDto>> Handle(SearchSuppliersQuery request, CancellationToken ct)
        {
            var query = unitOfWork.Repository<Supplier>().AsNoTracking();
            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var term = request.Search.Trim();
                query = query.Where(s => s.Name.Contains(term) || (s.Phone != null && s.Phone.StartsWith(term)));
            }

            return await PagedResult<SupplierDto>.CreateAsync(
                query.OrderBy(s => s.Name).Select(s => new SupplierDto(s.Id, s.Name, s.Phone, s.Email, s.Address)),
                request.SafePage, request.SafePageSize, ct);
        }
    }
}

namespace Inventory.Application.Features.V1.Suppliers.Commands
{
    using System.Text.Json.Serialization;
    using FluentValidation;
    using Inventory.Application.Features.V1.Suppliers.DTOs;
    using Inventory.Domain.Entities.Catalog;

    /// <summary>Tạo mới (Id = null) hoặc cập nhật nhà cung cấp.</summary>
    public sealed record SaveSupplierCommand(string Name, string? Phone, string? Email, string? Address) : IRequest<SupplierDto>
    {
        [JsonIgnore]
        public int? Id { get; init; }
    }

    public sealed class SaveSupplierCommandValidator : AbstractValidator<SaveSupplierCommand>
    {
        public SaveSupplierCommandValidator(IUnitOfWork<InventoryDbContext> unitOfWork)
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200)
                .MustAsync(async (cmd, name, ct) => !await unitOfWork.Repository<Supplier>()
                    .AnyAsync(s => s.Name == name.Trim() && s.Id != (cmd.Id ?? 0), ct))
                .WithMessage("Tên nhà cung cấp đã tồn tại.");
            RuleFor(x => x.Phone).MaximumLength(20).Matches(@"^[0-9+ ()-]*$").WithMessage("Số điện thoại không hợp lệ.");
            RuleFor(x => x.Email).MaximumLength(200).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
            RuleFor(x => x.Address).MaximumLength(300);
        }
    }

    public sealed class SaveSupplierCommandHandler(IUnitOfWork<InventoryDbContext> unitOfWork)
        : IRequestHandler<SaveSupplierCommand, SupplierDto>
    {
        public async Task<SupplierDto> Handle(SaveSupplierCommand request, CancellationToken ct)
        {
            Supplier supplier;
            if (request.Id is { } id)
            {
                supplier = await unitOfWork.Repository<Supplier>().FirstOrDefaultAsync(s => s.Id == id, ct)
                           ?? throw new NotFoundException("Supplier", id);
                supplier.Update(request.Name, request.Phone, request.Email, request.Address);
            }
            else
            {
                supplier = new Supplier(request.Name, request.Phone, request.Email, request.Address);
                unitOfWork.Repository<Supplier>().Add(supplier);
            }

            await unitOfWork.SaveChangesAsync(ct);
            return new SupplierDto(supplier.Id, supplier.Name, supplier.Phone, supplier.Email, supplier.Address);
        }
    }
}
