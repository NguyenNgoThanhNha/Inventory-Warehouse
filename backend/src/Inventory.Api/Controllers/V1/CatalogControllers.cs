using Inventory.Api.Authorization;
using Inventory.Application.Common.Models;
using Inventory.Application.Features.V1.Products.Commands;
using Inventory.Application.Features.V1.Products.DTOs;
using Inventory.Application.Features.V1.Products.Queries;
using Inventory.Application.Features.V1.Suppliers.Commands;
using Inventory.Application.Features.V1.Suppliers.DTOs;
using Inventory.Application.Features.V1.Suppliers.Queries;
using Inventory.Application.Features.V1.Warehouses.Commands;
using Inventory.Application.Features.V1.Warehouses.DTOs;
using Inventory.Application.Features.V1.Warehouses.Queries;
using Inventory.Domain.Constants;
using Inventory.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers.V1;

[Route("api/v1/products")]
public sealed class ProductsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [HasPermission(ConstActivity.Product, ActivityType.Read)]
    public async Task<ActionResult<PagedResult<ProductDto>>> Search([FromQuery] SearchProductsQuery query, CancellationToken ct) =>
        Ok(await Mediator.Send(query, ct));

    [HttpGet("{id:int}")]
    [HasPermission(ConstActivity.Product, ActivityType.Read)]
    public async Task<ActionResult<ProductDto>> Get(int id, CancellationToken ct) =>
        Ok(await Mediator.Send(new GetProductQuery(id), ct));

    [HttpPost]
    [HasPermission(ConstActivity.Product, ActivityType.Create)]
    public async Task<ActionResult<ProductDto>> Create(SaveProductCommand command, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await Mediator.Send(command with { Id = null }, ct));

    [HttpPut("{id:int}")]
    [HasPermission(ConstActivity.Product, ActivityType.Update)]
    public async Task<ActionResult<ProductDto>> Update(int id, SaveProductCommand command, CancellationToken ct) =>
        Ok(await Mediator.Send(command with { Id = id }, ct));

    [HttpDelete("{id:int}")]
    [HasPermission(ConstActivity.Product, ActivityType.Delete)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteProductCommand(id), ct);
        return NoContent();
    }
}

[Route("api/v1/product-groups")]
public sealed class ProductGroupsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [HasPermission(ConstActivity.Product, ActivityType.Read)]
    public async Task<ActionResult<IReadOnlyList<ProductGroupDto>>> GetAll(CancellationToken ct) =>
        Ok(await Mediator.Send(new GetProductGroupsQuery(), ct));

    [HttpPost]
    [HasPermission(ConstActivity.Product, ActivityType.Create)]
    public async Task<ActionResult<ProductGroupDto>> Create(SaveProductGroupCommand command, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await Mediator.Send(command with { Id = null }, ct));

    [HttpPut("{id:int}")]
    [HasPermission(ConstActivity.Product, ActivityType.Update)]
    public async Task<ActionResult<ProductGroupDto>> Update(int id, SaveProductGroupCommand command, CancellationToken ct) =>
        Ok(await Mediator.Send(command with { Id = id }, ct));
}

[Route("api/v1/warehouses")]
public sealed class WarehousesController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [HasPermission(ConstActivity.Warehouse, ActivityType.Read)]
    public async Task<ActionResult<IReadOnlyList<WarehouseDto>>> GetAll([FromQuery] bool includeInactive = false, CancellationToken ct = default) =>
        Ok(await Mediator.Send(new GetWarehousesQuery(includeInactive), ct));

    [HttpPost]
    [HasPermission(ConstActivity.Warehouse, ActivityType.Create)]
    public async Task<ActionResult<WarehouseDto>> Create(SaveWarehouseCommand command, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await Mediator.Send(command with { Id = null }, ct));

    [HttpPut("{id:int}")]
    [HasPermission(ConstActivity.Warehouse, ActivityType.Update)]
    public async Task<ActionResult<WarehouseDto>> Update(int id, SaveWarehouseCommand command, CancellationToken ct) =>
        Ok(await Mediator.Send(command with { Id = id }, ct));
}

[Route("api/v1/suppliers")]
public sealed class SuppliersController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [HasPermission(ConstActivity.Supplier, ActivityType.Read)]
    public async Task<ActionResult<PagedResult<SupplierDto>>> Search([FromQuery] SearchSuppliersQuery query, CancellationToken ct) =>
        Ok(await Mediator.Send(query, ct));

    [HttpPost]
    [HasPermission(ConstActivity.Supplier, ActivityType.Create)]
    public async Task<ActionResult<SupplierDto>> Create(SaveSupplierCommand command, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await Mediator.Send(command with { Id = null }, ct));

    [HttpPut("{id:int}")]
    [HasPermission(ConstActivity.Supplier, ActivityType.Update)]
    public async Task<ActionResult<SupplierDto>> Update(int id, SaveSupplierCommand command, CancellationToken ct) =>
        Ok(await Mediator.Send(command with { Id = id }, ct));
}
