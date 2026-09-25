using Inventory.Api.Authorization;
using Inventory.Application.Common.Interfaces;
using Inventory.Application.Common.Models;
using Inventory.Application.Features.V1.Exports.Queries;
using Inventory.Application.Features.V1.Imports.Commands.ImportProducts;
using Inventory.Application.Features.V1.Imports.DTOs;
using Inventory.Application.Features.V1.Imports.Queries;
using Inventory.Application.Features.V1.Stock.Services;
using Inventory.Application.Features.V1.StockAlerts.Commands.ScanLowStock;
using Inventory.Application.Features.V1.StockAlerts.DTOs;
using Inventory.Application.Features.V1.StockAlerts.Queries;
using Inventory.Domain.Constants;
using Inventory.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers.V1;

[Route("api/v1/imports")]
public sealed class ImportsController(ISender mediator) : ApiControllerBase(mediator)
{
    /// <summary>Giới hạn ở tầng HTTP lớn hơn giới hạn nghiệp vụ (5MB) một chút để validator trả lỗi dễ hiểu.</summary>
    private const long MaxRequestBytes = 6 * 1024 * 1024;

    /// <summary>
    /// Import sản phẩm từ Excel. <c>dryRun=true</c>: chỉ kiểm tra và báo lỗi từng dòng, không ghi.
    /// Cần CẢ quyền import (IMPORT_EXPORT:C) lẫn quyền thêm sản phẩm (PRODUCT:C) — nhiều [HasPermission] = AND.
    /// </summary>
    [HttpPost("products")]
    [HasPermission(ConstActivity.ImportExport, ActivityType.Create)]
    [HasPermission(ConstActivity.Product, ActivityType.Create)]
    [RequestSizeLimit(MaxRequestBytes)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ImportProductsResultDto>> Products(IFormFile file, [FromQuery] bool dryRun, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return Ok(await Mediator.Send(new ImportProductsCommand(stream, file.FileName, file.Length, dryRun), ct));
    }

    /// <summary>
    /// Đọc dòng hàng cho form lập phiếu từ Excel (không ghi gì).
    /// Chỉ cần đăng nhập ở đây: quyền lập đúng loại phiếu (C) được kiểm tra trong handler vì loại phiếu là tham số.
    /// </summary>
    [HttpPost("document-lines")]
    [RequestSizeLimit(MaxRequestBytes)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ParsedLinesDto>> DocumentLines(IFormFile file, [FromQuery] DocumentType type, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return Ok(await Mediator.Send(new ParseDocumentLinesQuery(stream, file.FileName, file.Length, type), ct));
    }

    /// <summary>File mẫu — chỉ cần đăng nhập (không chứa dữ liệu nào của hệ thống).</summary>
    [HttpGet("templates/{kind}")]
    public async Task<IActionResult> Template(ImportTemplateKind kind, CancellationToken ct) => Download(await Mediator.Send(new GetImportTemplateQuery(kind), ct));

    private FileContentResult Download(FileDto file) => File(file.Content, file.ContentType, file.FileName);
}

[Route("api/v1/exports")]
[HasPermission(ConstActivity.ImportExport, ActivityType.Read)]
[HasPermission(ConstActivity.StockReport, ActivityType.Read)]
public sealed class ExportsController(ISender mediator) : ApiControllerBase(mediator)
{
    /// <summary>Tồn kho ra Excel, cùng bộ lọc và thứ tự với <c>GET /stock</c>.</summary>
    [HttpGet("stock")]
    public async Task<IActionResult> Stock([FromQuery] int? warehouseId, [FromQuery] int? groupId, [FromQuery] string? search,
        [FromQuery] bool belowThreshold, [FromQuery] StockSort sort = StockSort.Sku, CancellationToken ct = default) =>
        Download(await Mediator.Send(new ExportStockQuery(warehouseId, groupId, search, belowThreshold, sort), ct));

    [HttpGet("kardex")]
    public async Task<IActionResult> Kardex([FromQuery] int productId, [FromQuery] int? warehouseId, [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to, CancellationToken ct) =>
        Download(await Mediator.Send(new ExportKardexQuery(productId, warehouseId, from, to), ct));

    private FileContentResult Download(FileDto file) => File(file.Content, file.ContentType, file.FileName);
}

[Route("api/v1/stock-alerts")]
public sealed class StockAlertsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [HasPermission(ConstActivity.StockReport, ActivityType.Read)]
    public async Task<ActionResult<PagedResult<StockAlertDto>>> Search([FromQuery] SearchStockAlertsQuery query, CancellationToken ct) =>
        Ok(await Mediator.Send(query, ct));

    /// <summary>Quét ngay (không chờ job định kỳ) — ví dụ vừa đổi hàng loạt ngưỡng.</summary>
    [HttpPost("scan")]
    [HasPermission(ConstActivity.Warehouse, ActivityType.Update)]
    public async Task<ActionResult<ScanLowStockResultDto>> Scan(CancellationToken ct) =>
        Ok(await Mediator.Send(new ScanLowStockCommand(), ct));
}
