using Inventory.Api.Authorization;
using Inventory.Application.Common.Models;
using Inventory.Application.Features.V1.Roles.Commands;
using Inventory.Application.Features.V1.Roles.DTOs;
using Inventory.Application.Features.V1.Roles.Queries;
using Inventory.Application.Features.V1.Users.Commands;
using Inventory.Application.Features.V1.Users.DTOs;
using Inventory.Application.Features.V1.Users.Queries;
using Inventory.Domain.Constants;
using Inventory.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers.V1;

/// <summary>Quản trị phân quyền 6 bảng — API chuẩn chung (chuẩn BE §4.4).</summary>
[Route("api/v1/activities")]
[HasPermission("ROLE:R|USER:R")]
public sealed class ActivitiesController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ActivityDto>>> GetAll(CancellationToken ct) =>
        Ok(await Mediator.Send(new GetActivitiesQuery(), ct));
}

[Route("api/v1/roles")]
public sealed class RolesController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [HasPermission("ROLE:R|USER:R")]
    public async Task<ActionResult<IReadOnlyList<RoleDto>>> GetAll(CancellationToken ct) =>
        Ok(await Mediator.Send(new GetRolesQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(ConstActivity.Role, ActivityType.Read)]
    public async Task<ActionResult<RoleDetailDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await Mediator.Send(new GetRoleDetailQuery(id), ct));

    [HttpPost]
    [HasPermission(ConstActivity.Role, ActivityType.Create)]
    public async Task<ActionResult<RoleDetailDto>> Create(SaveRoleCommand command, CancellationToken ct)
    {
        var role = await Mediator.Send(command with { Id = null }, ct);
        return CreatedAtAction(nameof(Get), new { id = role.Id }, role);
    }

    [HttpPut("{id:guid}")]
    [HasPermission(ConstActivity.Role, ActivityType.Update)]
    public async Task<ActionResult<RoleDetailDto>> Update(Guid id, SaveRoleCommand command, CancellationToken ct) =>
        Ok(await Mediator.Send(command with { Id = id }, ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(ConstActivity.Role, ActivityType.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteRoleCommand(id), ct);
        return NoContent();
    }
}

[Route("api/v1/users")]
public sealed class UsersController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [HasPermission(ConstActivity.User, ActivityType.Read)]
    public async Task<ActionResult<PagedResult<UserListItemDto>>> Search([FromQuery] SearchUsersQuery query, CancellationToken ct) =>
        Ok(await Mediator.Send(query, ct));

    [HttpPatch("{id:guid}")]
    [HasPermission(ConstActivity.User, ActivityType.Update)]
    public async Task<ActionResult<UserListItemDto>> UpdateStatus(Guid id, UpdateUserStatusCommand command, CancellationToken ct) =>
        Ok(await Mediator.Send(command with { Id = id }, ct));

    [HttpPut("{id:guid}/roles")]
    [HasPermission(ConstActivity.User, ActivityType.Update)]
    public async Task<ActionResult<UserListItemDto>> AssignRoles(Guid id, AssignUserRolesCommand command, CancellationToken ct) =>
        Ok(await Mediator.Send(command with { Id = id }, ct));

    [HttpGet("{id:guid}/permissions")]
    [HasPermission(ConstActivity.User, ActivityType.Read)]
    public async Task<ActionResult<UserPermissionDetailDto>> GetPermissions(Guid id, CancellationToken ct) =>
        Ok(await Mediator.Send(new GetUserPermissionsQuery(id), ct));

    [HttpPut("{id:guid}/permissions")]
    [HasPermission(ConstActivity.User, ActivityType.Update)]
    public async Task<ActionResult<UserPermissionDetailDto>> UpdatePermissions(Guid id, UpdateUserPermissionsCommand command,
        CancellationToken ct) =>
        Ok(await Mediator.Send(command with { Id = id }, ct));
}
