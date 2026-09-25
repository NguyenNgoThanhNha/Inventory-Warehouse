// Feature danh mục nhỏ: gom DTO + Commands + Queries vào các namespace chuẩn trong một file cho gọn.
namespace Inventory.Application.Features.V1.Products.DTOs
{
    public sealed record ProductDto(
        int Id, string Sku, string Name, string Unit, int GroupId, string GroupName,
        decimal Cost, decimal Price, string? ImageUrl, bool IsActive);

    public sealed record ProductGroupDto(int Id, string Name);
}

namespace Inventory.Application.Features.V1.Products.Services
{
    using Inventory.Application.Features.V1.Products.DTOs;
    using Inventory.Domain.Entities.Catalog;

    public static class ProductReader
    {
        public static IQueryable<ProductDto> Project(IQueryable<Product> query) =>
            query.Select(p => new ProductDto(p.Id, p.Sku, p.Name, p.Unit, p.GroupId, p.Group.Name, p.Cost, p.Price, p.ImageUrl, p.IsActive));

        public static async Task<ProductDto> GetAsync(IUnitOfWork<InventoryDbContext> unitOfWork, int id, CancellationToken ct) =>
            await Project(unitOfWork.Repository<Product>().AsNoTracking().Where(p => p.Id == id)).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Product", id);
    }
}

namespace Inventory.Application.Features.V1.Products.Queries
{
    using Inventory.Application.Features.V1.Products.DTOs;
    using Inventory.Application.Features.V1.Products.Services;
    using Inventory.Domain.Entities.Catalog;

    /// <summary>Danh sách sản phẩm (phân trang) — dùng cho màn danh mục và ô chọn sản phẩm trong phiếu.</summary>
    public sealed record SearchProductsQuery : PagedQuery, IRequest<PagedResult<ProductDto>>
    {
        /// <summary>Tìm theo SKU (bắt đầu bằng) hoặc tên (chứa).</summary>
        public string? Search { get; init; }
        public int? GroupId { get; init; }
        public bool? IsActive { get; init; }
    }

    public sealed class SearchProductsQueryHandler(IUnitOfWork<InventoryDbContext> unitOfWork)
        : IRequestHandler<SearchProductsQuery, PagedResult<ProductDto>>
    {
        public async Task<PagedResult<ProductDto>> Handle(SearchProductsQuery request, CancellationToken ct)
        {
            var query = unitOfWork.Repository<Product>().AsNoTracking();
            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var term = request.Search.Trim();
                var sku = Product.NormalizeSku(term);
                query = query.Where(p => p.Sku.StartsWith(sku) || p.Name.Contains(term));
            }
            if (request.GroupId is { } groupId) query = query.Where(p => p.GroupId == groupId);
            if (request.IsActive is { } active) query = query.Where(p => p.IsActive == active);

            return await PagedResult<ProductDto>.CreateAsync(
                ProductReader.Project(query.OrderBy(p => p.Sku)), request.SafePage, request.SafePageSize, ct);
        }
    }

    public sealed record GetProductQuery(int Id) : IRequest<ProductDto>;

    public sealed class GetProductQueryHandler(IUnitOfWork<InventoryDbContext> unitOfWork) : IRequestHandler<GetProductQuery, ProductDto>
    {
        public Task<ProductDto> Handle(GetProductQuery request, CancellationToken ct) => ProductReader.GetAsync(unitOfWork, request.Id, ct);
    }

    public sealed record GetProductGroupsQuery : IRequest<IReadOnlyList<ProductGroupDto>>;

    public sealed class GetProductGroupsQueryHandler(IUnitOfWork<InventoryDbContext> unitOfWork)
        : IRequestHandler<GetProductGroupsQuery, IReadOnlyList<ProductGroupDto>>
    {
        public async Task<IReadOnlyList<ProductGroupDto>> Handle(GetProductGroupsQuery request, CancellationToken ct) =>
            await unitOfWork.Repository<ProductGroup>().AsNoTracking().OrderBy(g => g.Name)
                .Select(g => new ProductGroupDto(g.Id, g.Name)).ToListAsync(ct);
    }
}

namespace Inventory.Application.Features.V1.Products.Commands
{
    using System.Text.Json.Serialization;
    using FluentValidation;
    using Inventory.Application.Features.V1.Products.DTOs;
    using Inventory.Application.Features.V1.Products.Services;
    using Inventory.Domain.Entities.Catalog;
    using Inventory.Domain.Entities.Stock;

    /// <summary>Tạo mới (Id = null) hoặc cập nhật sản phẩm. SKU chỉ đặt lúc tạo — cập nhật bỏ qua SKU.</summary>
    public sealed record SaveProductCommand(
        string Sku, string Name, string Unit, int GroupId, decimal Cost, decimal Price, string? ImageUrl, bool IsActive = true)
        : IRequest<ProductDto>
    {
        [JsonIgnore]
        public int? Id { get; init; }
    }

    public sealed class SaveProductCommandValidator : AbstractValidator<SaveProductCommand>
    {
        public SaveProductCommandValidator(IUnitOfWork<InventoryDbContext> unitOfWork)
        {
            RuleFor(x => x.Sku).NotEmpty().MaximumLength(50).Matches("^[A-Za-z0-9._-]+$")
                .WithMessage("SKU chỉ gồm chữ, số và . _ -")
                .MustAsync(async (sku, ct) =>
                {
                    var normalized = Product.NormalizeSku(sku);
                    return !await unitOfWork.Repository<Product>().AnyAsync(p => p.Sku == normalized, ct);
                })
                .When(x => x.Id is null)
                .WithMessage("SKU đã tồn tại.");
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Unit).NotEmpty().MaximumLength(20);
            RuleFor(x => x.Cost).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
            RuleFor(x => x.ImageUrl).MaximumLength(500);
            RuleFor(x => x.GroupId)
                .MustAsync((id, ct) => unitOfWork.Repository<ProductGroup>().AnyAsync(g => g.Id == id, ct))
                .WithMessage("Nhóm hàng không tồn tại.");
        }
    }

    public sealed class SaveProductCommandHandler(IUnitOfWork<InventoryDbContext> unitOfWork) : IRequestHandler<SaveProductCommand, ProductDto>
    {
        public async Task<ProductDto> Handle(SaveProductCommand request, CancellationToken ct)
        {
            Product product;
            if (request.Id is { } id)
            {
                product = await unitOfWork.Repository<Product>().FirstOrDefaultAsync(p => p.Id == id, ct)
                          ?? throw new NotFoundException("Product", id);
                product.Update(request.Name, request.Unit, request.GroupId, request.Cost, request.Price, request.ImageUrl, request.IsActive);
            }
            else
            {
                product = new Product(request.Sku, request.Name, request.Unit, request.GroupId, request.Cost, request.Price, request.ImageUrl);
                unitOfWork.Repository<Product>().Add(product);
            }

            await unitOfWork.SaveChangesAsync(ct);
            return await ProductReader.GetAsync(unitOfWork, product.Id, ct);
        }
    }

    /// <summary>Xóa mềm. Sản phẩm đã phát sinh chứng từ thì không xóa được — chuyển sang ngừng kinh doanh.</summary>
    public sealed record DeleteProductCommand(int Id) : IRequest;

    public sealed class DeleteProductCommandHandler(IUnitOfWork<InventoryDbContext> unitOfWork) : IRequestHandler<DeleteProductCommand>
    {
        public async Task Handle(DeleteProductCommand request, CancellationToken ct)
        {
            var product = await unitOfWork.Repository<Product>().FirstOrDefaultAsync(p => p.Id == request.Id, ct)
                          ?? throw new NotFoundException("Product", request.Id);

            if (await unitOfWork.Repository<StockDocumentLine>().AnyAsync(l => l.ProductId == product.Id, ct))
                throw new ConflictException("Sản phẩm đã phát sinh chứng từ, không xóa được. Hãy chuyển sang ngừng kinh doanh.");

            unitOfWork.Repository<Product>().Remove(product);
            await unitOfWork.SaveChangesAsync(ct);
        }
    }

    /// <summary>Tạo mới (Id = null) hoặc đổi tên nhóm hàng.</summary>
    public sealed record SaveProductGroupCommand(string Name) : IRequest<ProductGroupDto>
    {
        [JsonIgnore]
        public int? Id { get; init; }
    }

    public sealed class SaveProductGroupCommandValidator : AbstractValidator<SaveProductGroupCommand>
    {
        public SaveProductGroupCommandValidator(IUnitOfWork<InventoryDbContext> unitOfWork)
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(100)
                .MustAsync(async (cmd, name, ct) => !await unitOfWork.Repository<ProductGroup>()
                    .AnyAsync(g => g.Name == name.Trim() && g.Id != (cmd.Id ?? 0), ct))
                .WithMessage("Tên nhóm hàng đã tồn tại.");
        }
    }

    public sealed class SaveProductGroupCommandHandler(IUnitOfWork<InventoryDbContext> unitOfWork)
        : IRequestHandler<SaveProductGroupCommand, ProductGroupDto>
    {
        public async Task<ProductGroupDto> Handle(SaveProductGroupCommand request, CancellationToken ct)
        {
            ProductGroup group;
            if (request.Id is { } id)
            {
                group = await unitOfWork.Repository<ProductGroup>().FirstOrDefaultAsync(g => g.Id == id, ct)
                        ?? throw new NotFoundException("ProductGroup", id);
                group.Rename(request.Name);
            }
            else
            {
                group = new ProductGroup(request.Name);
                unitOfWork.Repository<ProductGroup>().Add(group);
            }

            await unitOfWork.SaveChangesAsync(ct);
            return new ProductGroupDto(group.Id, group.Name);
        }
    }
}
