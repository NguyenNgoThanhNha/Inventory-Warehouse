using Inventory.Api.Authorization;
using Inventory.Application.Common.Models;
using Inventory.Application.Features.V1.Reports.DTOs;
using Inventory.Application.Features.V1.Reports.Queries.GetDashboard;
using Inventory.Application.Features.V1.Reports.Queries.GetKardex;
using Inventory.Application.Features.V1.Stock.Commands;
using Inventory.Application.Features.V1.Stock.DTOs;
using Inventory.Application.Features.V1.Stock.Queries;
using Inventory.Application.Features.V1.StockDocuments.Commands.CancelStockDocument;
using Inventory.Application.Features.V1.StockDocuments.Commands.CreateGoodsIssue;
using Inventory.Application.Features.V1.StockDocuments.Commands.CreateGoodsReceipt;
using Inventory.Application.Features.V1.StockDocuments.Commands.CreateStockTake;
using Inventory.Application.Features.V1.StockDocuments.Commands.CreateTransfer;
using Inventory.Application.Features.V1.StockDocuments.Commands.PostStockDocument;
using Inventory.Application.Features.V1.StockDocuments.DTOs;
using Inventory.Application.Features.V1.StockDocuments.Queries;
using Inventory.Domain.Constants;
using Inventory.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers.V1;

[Route("api/v1/stock")]
public sealed class StockController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [HasPermission(ConstActivity.StockReport, ActivityType.Read)]
    public async Task<ActionResult<PagedResult<StockRowDto>>> Search([FromQuery] SearchStockQuery query, CancellationToken ct) =>
        Ok(await Mediator.Send(query, ct));

    /// <summary>Tồn hiện tại tại một kho cho các sản phẩm: <c>?warehouseId=1&amp;productIds=3&amp;productIds=7</c>.</summary>
    [HttpGet("available")]
    [HasPermission(ConstActivity.StockReport, ActivityType.Read)]
    public async Task<ActionResult<IReadOnlyList<AvailableStockDto>>> Available(
        [FromQuery] int warehouseId, [FromQuery] int[] productIds, CancellationToken ct) =>
        Ok(await Mediator.Send(new GetAvailableStockQuery(warehouseId, productIds), ct));

    [HttpPut("threshold")]
    [HasPermission(ConstActivity.Warehouse, ActivityType.Update)]
    public async Task<ActionResult<StockThresholdDto>> SetThreshold(SetStockThresholdCommand command, CancellationToken ct) =>
        Ok(await Mediator.Send(command, ct));
}

// Bốn controller chứng từ giống nhau về hình dạng; tách riêng để mỗi action có [HasPermission] đúng activity của loại phiếu.
// C = lập phiếu nháp · R = xem · U = duyệt/ghi sổ (đổi tồn) · D = hủy phiếu nháp.
// Tạo phiếu kèm "post": true cần thêm quyền U — kiểm tra trong StockDocumentWriter.

[Route("api/v1/goods-receipts")]
public sealed class GoodsReceiptsController(ISender mediator) : ApiControllerBase(mediator)
{
    private const DocumentType Type = DocumentType.GoodsReceipt;

    [HttpGet]
    [HasPermission(ConstActivity.GoodsReceipt, ActivityType.Read)]
    public async Task<ActionResult<PagedResult<StockDocumentListItemDto>>> Search([FromQuery] SearchStockDocumentsQuery query, CancellationToken ct) =>
        Ok(await Mediator.Send(query with { Type = Type }, ct));

    [HttpGet("{id:int}")]
    [HasPermission(ConstActivity.GoodsReceipt, ActivityType.Read)]
    public async Task<ActionResult<StockDocumentDto>> Get(int id, CancellationToken ct) =>
        Ok(await Mediator.Send(new GetStockDocumentQuery(id, Type), ct));

    [HttpPost]
    [HasPermission(ConstActivity.GoodsReceipt, ActivityType.Create)]
    public async Task<ActionResult<StockDocumentDto>> Create(CreateGoodsReceiptCommand command,
        [FromHeader(Name = ConstHeader.IdempotencyKey)] string? idempotencyKey, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await Mediator.Send(command with { IdempotencyKey = idempotencyKey }, ct));

    [HttpPost("{id:int}/post")]
    [HasPermission(ConstActivity.GoodsReceipt, ActivityType.Update)]
    public async Task<ActionResult<StockDocumentDto>> Post(int id, PostStockDocumentCommand command, CancellationToken ct) =>
        Ok(await Mediator.Send(command with { Id = id, Type = Type }, ct));

    [HttpPost("{id:int}/cancel")]
    [HasPermission(ConstActivity.GoodsReceipt, ActivityType.Delete)]
    public async Task<ActionResult<StockDocumentDto>> Cancel(int id, CancelStockDocumentCommand command, CancellationToken ct) =>
        Ok(await Mediator.Send(command with { Id = id, Type = Type }, ct));
}

[Route("api/v1/goods-issues")]
public sealed class GoodsIssuesController(ISender mediator) : ApiControllerBase(mediator)
{
    private const DocumentType Type = DocumentType.GoodsIssue;

    [HttpGet]
    [HasPermission(ConstActivity.GoodsIssue, ActivityType.Read)]
    public async Task<ActionResult<PagedResult<StockDocumentListItemDto>>> Search([FromQuery] SearchStockDocumentsQuery query, CancellationToken ct) =>
        Ok(await Mediator.Send(query with { Type = Type }, ct));

    [HttpGet("{id:int}")]
    [HasPermission(ConstActivity.GoodsIssue, ActivityType.Read)]
    public async Task<ActionResult<StockDocumentDto>> Get(int id, CancellationToken ct) =>
        Ok(await Mediator.Send(new GetStockDocumentQuery(id, Type), ct));

    [HttpPost]
    [HasPermission(ConstActivity.GoodsIssue, ActivityType.Create)]
    public async Task<ActionResult<StockDocumentDto>> Create(CreateGoodsIssueCommand command,
        [FromHeader(Name = ConstHeader.IdempotencyKey)] string? idempotencyKey, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await Mediator.Send(command with { IdempotencyKey = idempotencyKey }, ct));

    [HttpPost("{id:int}/post")]
    [HasPermission(ConstActivity.GoodsIssue, ActivityType.Update)]
    public async Task<ActionResult<StockDocumentDto>> Post(int id, PostStockDocumentCommand command, CancellationToken ct) =>
        Ok(await Mediator.Send(command with { Id = id, Type = Type }, ct));

    [HttpPost("{id:int}/cancel")]
    [HasPermission(ConstActivity.GoodsIssue, ActivityType.Delete)]
    public async Task<ActionResult<StockDocumentDto>> Cancel(int id, CancelStockDocumentCommand command, CancellationToken ct) =>
        Ok(await Mediator.Send(command with { Id = id, Type = Type }, ct));
}

[Route("api/v1/transfers")]
public sealed class TransfersController(ISender mediator) : ApiControllerBase(mediator)
{
    private const DocumentType Type = DocumentType.Transfer;

    [HttpGet]
    [HasPermission(ConstActivity.Transfer, ActivityType.Read)]
    public async Task<ActionResult<PagedResult<StockDocumentListItemDto>>> Search([FromQuery] SearchStockDocumentsQuery query, CancellationToken ct) =>
        Ok(await Mediator.Send(query with { Type = Type }, ct));

    [HttpGet("{id:int}")]
    [HasPermission(ConstActivity.Transfer, ActivityType.Read)]
    public async Task<ActionResult<StockDocumentDto>> Get(int id, CancellationToken ct) =>
        Ok(await Mediator.Send(new GetStockDocumentQuery(id, Type), ct));

    [HttpPost]
    [HasPermission(ConstActivity.Transfer, ActivityType.Create)]
    public async Task<ActionResult<StockDocumentDto>> Create(CreateTransferCommand command,
        [FromHeader(Name = ConstHeader.IdempotencyKey)] string? idempotencyKey, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await Mediator.Send(command with { IdempotencyKey = idempotencyKey }, ct));

    [HttpPost("{id:int}/post")]
    [HasPermission(ConstActivity.Transfer, ActivityType.Update)]
    public async Task<ActionResult<StockDocumentDto>> Post(int id, PostStockDocumentCommand command, CancellationToken ct) =>
        Ok(await Mediator.Send(command with { Id = id, Type = Type }, ct));

    [HttpPost("{id:int}/cancel")]
    [HasPermission(ConstActivity.Transfer, ActivityType.Delete)]
    public async Task<ActionResult<StockDocumentDto>> Cancel(int id, CancelStockDocumentCommand command, CancellationToken ct) =>
        Ok(await Mediator.Send(command with { Id = id, Type = Type }, ct));
}

[Route("api/v1/stock-takes")]
public sealed class StockTakesController(ISender mediator) : ApiControllerBase(mediator)
{
    private const DocumentType Type = DocumentType.StockTake;

    [HttpGet]
    [HasPermission(ConstActivity.StockTake, ActivityType.Read)]
    public async Task<ActionResult<PagedResult<StockDocumentListItemDto>>> Search([FromQuery] SearchStockDocumentsQuery query, CancellationToken ct) =>
        Ok(await Mediator.Send(query with { Type = Type }, ct));

    [HttpGet("{id:int}")]
    [HasPermission(ConstActivity.StockTake, ActivityType.Read)]
    public async Task<ActionResult<StockDocumentDto>> Get(int id, CancellationToken ct) =>
        Ok(await Mediator.Send(new GetStockDocumentQuery(id, Type), ct));

    [HttpPost]
    [HasPermission(ConstActivity.StockTake, ActivityType.Create)]
    public async Task<ActionResult<StockDocumentDto>> Create(CreateStockTakeCommand command,
        [FromHeader(Name = ConstHeader.IdempotencyKey)] string? idempotencyKey, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await Mediator.Send(command with { IdempotencyKey = idempotencyKey }, ct));

    [HttpPost("{id:int}/post")]
    [HasPermission(ConstActivity.StockTake, ActivityType.Update)]
    public async Task<ActionResult<StockDocumentDto>> Post(int id, PostStockDocumentCommand command, CancellationToken ct) =>
        Ok(await Mediator.Send(command with { Id = id, Type = Type }, ct));

    [HttpPost("{id:int}/cancel")]
    [HasPermission(ConstActivity.StockTake, ActivityType.Delete)]
    public async Task<ActionResult<StockDocumentDto>> Cancel(int id, CancelStockDocumentCommand command, CancellationToken ct) =>
        Ok(await Mediator.Send(command with { Id = id, Type = Type }, ct));
}

[Route("api/v1/reports")]
[HasPermission(ConstActivity.StockReport, ActivityType.Read)]
public sealed class ReportsController(ISender mediator) : ApiControllerBase(mediator)
{
    /// <summary>Tổng quan kho: KPI, giá trị tồn theo kho/nhóm, hàng sắp hết, nhập–xuất 30 ngày, hàng chậm luân chuyển.</summary>
    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardDto>> Dashboard([FromQuery] int? warehouseId, CancellationToken ct) =>
        Ok(await Mediator.Send(new GetDashboardQuery(warehouseId), ct));

    /// <summary>Sổ nhập – xuất – tồn: <c>?productId=&amp;warehouseId=&amp;from=yyyy-MM-dd&amp;to=yyyy-MM-dd&amp;page=&amp;pageSize=</c>.</summary>
    [HttpGet("kardex")]
    public async Task<ActionResult<KardexDto>> Kardex([FromQuery] GetKardexQuery query, CancellationToken ct) =>
        Ok(await Mediator.Send(query, ct));
}
