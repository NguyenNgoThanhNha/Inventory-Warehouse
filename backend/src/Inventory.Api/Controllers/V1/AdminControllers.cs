using Inventory.Api.Authorization;
using Inventory.Application.Common.Models;
using Inventory.Application.Features.V1.ApiLogs.DTOs;
using Inventory.Application.Features.V1.ApiLogs.Queries;
using Inventory.Application.Features.V1.Notifications.Commands;
using Inventory.Application.Features.V1.Notifications.DTOs;
using Inventory.Application.Features.V1.Notifications.Queries;
using Inventory.Domain.Constants;
using Inventory.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers.V1;

[Route("api/v1/notifications")]
public sealed class NotificationsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotificationDto>>> Get([FromQuery] bool unreadOnly = false, [FromQuery] int take = 20,
        CancellationToken ct = default) =>
        Ok(await Mediator.Send(new GetNotificationsQuery(unreadOnly, take), ct));

    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCountDto>> UnreadCount(CancellationToken ct) =>
        Ok(await Mediator.Send(new GetUnreadCountQuery(), ct));

    [HttpPost("{id:long}/read")]
    public async Task<IActionResult> MarkRead(long id, CancellationToken ct)
    {
        await Mediator.Send(new MarkNotificationsReadCommand(id), ct);
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await Mediator.Send(new MarkNotificationsReadCommand(null), ct);
        return NoContent();
    }
}

/// <summary>Tra cứu log request/response API để debug (chuẩn BE §9.2).</summary>
[Route("api/v1/api-logs")]
[HasPermission(ConstActivity.ApiLog, ActivityType.Read)]
public sealed class ApiLogsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<ApiLogListItemDto>>> Search([FromQuery] SearchApiLogsQuery query, CancellationToken ct) =>
        Ok(await Mediator.Send(query, ct));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ApiLogDetailDto>> Get(long id, CancellationToken ct) =>
        Ok(await Mediator.Send(new GetApiLogDetailQuery(id), ct));
}
