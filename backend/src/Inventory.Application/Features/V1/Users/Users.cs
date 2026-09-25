namespace Inventory.Application.Features.V1.Users.DTOs
{
    using Inventory.Application.Features.V1.Roles.DTOs;

    public sealed record UserListItemDto(
        Guid Id, string Email, string FullName, bool IsActive, IReadOnlyList<RoleRefDto> Roles, DateTime CreatedDate);

    public sealed record UserPermissionDetailDto(
        Guid UserId,
        bool IsAdmin,
        IReadOnlyList<RoleRefDto> Roles,
        IReadOnlyList<ActivityPermissionDto> UserActivities,
        IReadOnlyList<ActivityPermissionDto> Effective);
}

namespace Inventory.Application.Features.V1.Users.Services
{
    using Inventory.Application.Features.V1.Roles.DTOs;
    using Inventory.Application.Features.V1.Roles.Services;
    using Inventory.Application.Features.V1.Users.DTOs;
    using Inventory.Domain.Entities.Sys;

    public static class UserReader
    {
        public static IQueryable<UserListItemDto> Project(IQueryable<SysAccount> query) =>
            query.Select(u => new UserListItemDto(u.Id, u.Email, u.FullName, u.IsActive,
                u.UserRoles.Select(ur => new RoleRefDto(ur.Role.Id, ur.Role.Name)).ToList(), u.CreatedDate));

        public static async Task<UserListItemDto> GetAsync(IUnitOfWork<InventoryDbContext> unitOfWork, Guid userId, CancellationToken ct) =>
            await Project(unitOfWork.Repository<SysAccount>().AsNoTracking().Where(u => u.Id == userId)).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("User", userId);

        public static async Task<UserPermissionDetailDto> GetPermissionsAsync(
            IUnitOfWork<InventoryDbContext> unitOfWork, IPermissionService permissionService, Guid userId, CancellationToken ct)
        {
            var user = await GetAsync(unitOfWork, userId, ct);
            var own = await unitOfWork.Repository<SysUserActivity>().AsNoTracking().Where(ua => ua.UserId == userId).ToListAsync(ct);
            var effective = await permissionService.GetEffectiveAsync(userId, ct);
            var activities = await unitOfWork.Repository<SysActivity>().AsNoTracking().OrderBy(a => a.Code).ToListAsync(ct);

            var effectiveDtos = activities
                .Where(a => effective.Activities.TryGetValue(a.Code, out var f) && f.Any)
                .Select(a =>
                {
                    var f = effective.Activities[a.Code];
                    return new ActivityPermissionDto(a.Id, a.Code, a.Name, f.C, f.R, f.U, f.D);
                })
                .ToList();

            return new UserPermissionDetailDto(userId, effective.IsAdmin, user.Roles,
                await PermissionMatrix.ToDtosAsync(unitOfWork, own, ct), effectiveDtos);
        }

        /// <summary>Không cho khóa / gỡ quyền Admin của Admin active cuối cùng.</summary>
        public static async Task EnsureNotLastAdminAsync(IUnitOfWork<InventoryDbContext> unitOfWork, Guid userId, CancellationToken ct)
        {
            var otherAdmins = await unitOfWork.Repository<SysAccount>().CountAsync(u =>
                u.Id != userId && u.IsActive && u.UserRoles.Any(ur => ur.Role.RoleType == ConstRole.AdminRoleType), ct);
            if (otherAdmins == 0) throw new ConflictException("Hệ thống phải còn ít nhất một Admin đang hoạt động.");
        }

        public static Task<bool> IsAdminAsync(IUnitOfWork<InventoryDbContext> unitOfWork, Guid userId, CancellationToken ct) =>
            unitOfWork.Repository<SysUserRole>().AnyAsync(ur => ur.UserId == userId && ur.Role.RoleType == ConstRole.AdminRoleType, ct);
    }
}

namespace Inventory.Application.Features.V1.Users.Queries
{
    using Inventory.Application.Features.V1.Users.DTOs;
    using Inventory.Application.Features.V1.Users.Services;
    using Inventory.Domain.Entities.Sys;

    public sealed record SearchUsersQuery : PagedQuery, IRequest<PagedResult<UserListItemDto>>
    {
        public string? Search { get; init; }
        public Guid? RoleId { get; init; }
    }

    public sealed class SearchUsersQueryHandler(IUnitOfWork<InventoryDbContext> unitOfWork)
        : IRequestHandler<SearchUsersQuery, PagedResult<UserListItemDto>>
    {
        public async Task<PagedResult<UserListItemDto>> Handle(SearchUsersQuery request, CancellationToken ct)
        {
            var query = unitOfWork.Repository<SysAccount>().AsNoTracking();
            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var term = request.Search.Trim();
                query = query.Where(u => u.Email.Contains(term) || u.FullName.Contains(term));
            }
            if (request.RoleId is { } roleId) query = query.Where(u => u.UserRoles.Any(ur => ur.RoleId == roleId));

            return await PagedResult<UserListItemDto>.CreateAsync(
                UserReader.Project(query.OrderBy(u => u.FullName)), request.SafePage, request.SafePageSize, ct);
        }
    }

    public sealed record GetUserPermissionsQuery(Guid UserId) : IRequest<UserPermissionDetailDto>;

    public sealed class GetUserPermissionsQueryHandler(IUnitOfWork<InventoryDbContext> unitOfWork, IPermissionService permissionService)
        : IRequestHandler<GetUserPermissionsQuery, UserPermissionDetailDto>
    {
        public Task<UserPermissionDetailDto> Handle(GetUserPermissionsQuery request, CancellationToken ct) =>
            UserReader.GetPermissionsAsync(unitOfWork, permissionService, request.UserId, ct);
    }
}

namespace Inventory.Application.Features.V1.Users.Commands
{
    using System.Text.Json.Serialization;
    using FluentValidation;
    using Inventory.Application.Features.V1.Roles.DTOs;
    using Inventory.Application.Features.V1.Roles.Services;
    using Inventory.Application.Features.V1.Users.DTOs;
    using Inventory.Application.Features.V1.Users.Services;
    using Inventory.Domain.Entities.Sys;

    public sealed record UpdateUserStatusCommand(bool IsActive) : IRequest<UserListItemDto>
    {
        [JsonIgnore]
        public Guid Id { get; init; }
    }

    public sealed class UpdateUserStatusCommandHandler(
        IUnitOfWork<InventoryDbContext> unitOfWork, ICurrentUser currentUser, IPermissionService permissionService, TimeProvider clock)
        : IRequestHandler<UpdateUserStatusCommand, UserListItemDto>
    {
        public async Task<UserListItemDto> Handle(UpdateUserStatusCommand request, CancellationToken ct)
        {
            var user = await unitOfWork.Repository<SysAccount>().FirstOrDefaultAsync(u => u.Id == request.Id, ct)
                       ?? throw new NotFoundException("User", request.Id);

            if (!request.IsActive)
            {
                if (user.Id == currentUser.UserId) throw new ConflictException("Không thể tự khóa tài khoản của chính mình.");
                if (await UserReader.IsAdminAsync(unitOfWork, user.Id, ct)) await UserReader.EnsureNotLastAdminAsync(unitOfWork, user.Id, ct);

                var now = clock.GetUtcNow().UtcDateTime;
                var sessions = await unitOfWork.Repository<SysRefreshToken>()
                    .Where(t => t.UserId == user.Id && t.RevokedAt == null).ToListAsync(ct);
                sessions.ForEach(t => t.Revoke(now));
            }

            user.IsActive = request.IsActive;
            await unitOfWork.SaveChangesAsync(ct);
            permissionService.Invalidate(user.Id);

            return await UserReader.GetAsync(unitOfWork, user.Id, ct);
        }
    }

    public sealed record AssignUserRolesCommand(IReadOnlyList<Guid> RoleIds) : IRequest<UserListItemDto>
    {
        [JsonIgnore]
        public Guid Id { get; init; }
    }

    public sealed class AssignUserRolesCommandValidator : AbstractValidator<AssignUserRolesCommand>
    {
        public AssignUserRolesCommandValidator(IUnitOfWork<InventoryDbContext> unitOfWork)
        {
            RuleFor(x => x.RoleIds).NotNull()
                .MustAsync(async (ids, ct) =>
                {
                    var distinct = ids.Distinct().ToList();
                    return await unitOfWork.Repository<SysRole>().CountAsync(r => distinct.Contains(r.Id), ct) == distinct.Count;
                })
                .WithMessage("Có role không tồn tại.");
        }
    }

    public sealed class AssignUserRolesCommandHandler(
        IUnitOfWork<InventoryDbContext> unitOfWork, ICurrentUser currentUser, IPermissionService permissionService)
        : IRequestHandler<AssignUserRolesCommand, UserListItemDto>
    {
        public async Task<UserListItemDto> Handle(AssignUserRolesCommand request, CancellationToken ct)
        {
            if (!await unitOfWork.Repository<SysAccount>().AnyAsync(u => u.Id == request.Id, ct))
                throw new NotFoundException("User", request.Id);

            var userRoles = unitOfWork.Repository<SysUserRole>();
            var current = await userRoles.Include(ur => ur.Role).Where(ur => ur.UserId == request.Id).ToListAsync(ct);
            var wanted = request.RoleIds.Distinct().ToHashSet();

            var losingAdmin = current.Any(ur => ur.Role.IsAdmin && !wanted.Contains(ur.RoleId));
            if (losingAdmin)
            {
                if (request.Id == currentUser.UserId) throw new ConflictException("Không thể tự gỡ role Admin của chính mình.");
                await UserReader.EnsureNotLastAdminAsync(unitOfWork, request.Id, ct);
            }

            userRoles.RemoveRange(current.Where(ur => !wanted.Contains(ur.RoleId)));
            foreach (var roleId in wanted.Where(id => current.All(ur => ur.RoleId != id)))
                userRoles.Add(new SysUserRole { UserId = request.Id, RoleId = roleId });

            await unitOfWork.SaveChangesAsync(ct);
            permissionService.Invalidate(request.Id);

            return await UserReader.GetAsync(unitOfWork, request.Id, ct);
        }
    }

    /// <summary>Thay toàn bộ quyền riêng (Sys_UserActivity) của một tài khoản.</summary>
    public sealed record UpdateUserPermissionsCommand(IReadOnlyList<ActivityPermissionInput> Activities) : IRequest<UserPermissionDetailDto>
    {
        [JsonIgnore]
        public Guid Id { get; init; }
    }

    public sealed class UpdateUserPermissionsCommandValidator : AbstractValidator<UpdateUserPermissionsCommand>
    {
        public UpdateUserPermissionsCommandValidator(IUnitOfWork<InventoryDbContext> unitOfWork) =>
            RuleFor(x => x.Activities).NotNull().SetValidator(new ActivityPermissionInputsValidator(unitOfWork));
    }

    public sealed class UpdateUserPermissionsCommandHandler(IUnitOfWork<InventoryDbContext> unitOfWork, IPermissionService permissionService)
        : IRequestHandler<UpdateUserPermissionsCommand, UserPermissionDetailDto>
    {
        public async Task<UserPermissionDetailDto> Handle(UpdateUserPermissionsCommand request, CancellationToken ct)
        {
            if (!await unitOfWork.Repository<SysAccount>().AnyAsync(u => u.Id == request.Id, ct))
                throw new NotFoundException("User", request.Id);

            var existing = await unitOfWork.Repository<SysUserActivity>().Where(ua => ua.UserId == request.Id).ToListAsync(ct);
            PermissionMatrix.Apply(existing, request.Activities,
                activityId => new SysUserActivity { UserId = request.Id, ActivityId = activityId },
                unitOfWork.Repository<SysUserActivity>());

            await unitOfWork.SaveChangesAsync(ct);
            permissionService.Invalidate(request.Id);

            return await UserReader.GetPermissionsAsync(unitOfWork, permissionService, request.Id, ct);
        }
    }
}
