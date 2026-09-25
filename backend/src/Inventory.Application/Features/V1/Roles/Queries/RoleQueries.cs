using Inventory.Application.Features.V1.Roles.DTOs;
using Inventory.Application.Features.V1.Roles.Services;
using Inventory.Domain.Entities.Sys;

namespace Inventory.Application.Features.V1.Roles.Queries;

public sealed record GetActivitiesQuery : IRequest<IReadOnlyList<ActivityDto>>;

public sealed class GetActivitiesQueryHandler(IUnitOfWork<InventoryDbContext> unitOfWork)
    : IRequestHandler<GetActivitiesQuery, IReadOnlyList<ActivityDto>>
{
    public async Task<IReadOnlyList<ActivityDto>> Handle(GetActivitiesQuery request, CancellationToken ct) =>
        await unitOfWork.Repository<SysActivity>().AsNoTracking()
            .OrderBy(a => a.Code)
            .Select(a => new ActivityDto(a.Id, a.Code, a.Name, a.Description))
            .ToListAsync(ct);
}

public sealed record GetRolesQuery : IRequest<IReadOnlyList<RoleDto>>;

public sealed class GetRolesQueryHandler(IUnitOfWork<InventoryDbContext> unitOfWork) : IRequestHandler<GetRolesQuery, IReadOnlyList<RoleDto>>
{
    public async Task<IReadOnlyList<RoleDto>> Handle(GetRolesQuery request, CancellationToken ct) =>
        await unitOfWork.Repository<SysRole>().AsNoTracking()
            .OrderByDescending(r => r.RoleType == ConstRole.AdminRoleType).ThenBy(r => r.Name)
            .Select(r => new RoleDto(r.Id, r.Name, r.Description, r.RoleType == ConstRole.AdminRoleType, r.UserRoles.Count))
            .ToListAsync(ct);
}

public sealed record GetRoleDetailQuery(Guid Id) : IRequest<RoleDetailDto>;

public sealed class GetRoleDetailQueryHandler(IUnitOfWork<InventoryDbContext> unitOfWork) : IRequestHandler<GetRoleDetailQuery, RoleDetailDto>
{
    public async Task<RoleDetailDto> Handle(GetRoleDetailQuery request, CancellationToken ct) =>
        await RoleDetailReader.ReadAsync(unitOfWork, request.Id, ct);
}

public static class RoleDetailReader
{
    public static async Task<RoleDetailDto> ReadAsync(IUnitOfWork<InventoryDbContext> unitOfWork, Guid roleId, CancellationToken ct)
    {
        var role = await unitOfWork.Repository<SysRole>().AsNoTracking()
                       .Where(r => r.Id == roleId)
                       .Select(r => new { r.Id, r.Name, r.Description, r.RoleType, UserCount = r.UserRoles.Count })
                       .FirstOrDefaultAsync(ct)
                   ?? throw new NotFoundException("Role", roleId);

        var rows = await unitOfWork.Repository<SysRoleActivity>().AsNoTracking().Where(ra => ra.RoleId == roleId).ToListAsync(ct);
        var isAdmin = role.RoleType == ConstRole.AdminRoleType;
        return new RoleDetailDto(role.Id, role.Name, role.Description, isAdmin, role.UserCount,
            isAdmin ? [] : await PermissionMatrix.ToDtosAsync(unitOfWork, rows, ct));
    }
}
