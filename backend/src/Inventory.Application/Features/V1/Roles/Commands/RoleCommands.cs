using System.Text.Json.Serialization;
using FluentValidation;
using Inventory.Application.Features.V1.Roles.DTOs;
using Inventory.Application.Features.V1.Roles.Queries;
using Inventory.Application.Features.V1.Roles.Services;
using Inventory.Domain.Entities.Sys;

namespace Inventory.Application.Features.V1.Roles.Commands;

/// <summary>Tạo (Id = null) hoặc sửa role cùng ma trận quyền.</summary>
public sealed record SaveRoleCommand(string Name, string? Description, IReadOnlyList<ActivityPermissionInput> Activities)
    : IRequest<RoleDetailDto>
{
    [JsonIgnore]
    public Guid? Id { get; init; }
}

public sealed class SaveRoleCommandValidator : AbstractValidator<SaveRoleCommand>
{
    public SaveRoleCommandValidator(IUnitOfWork<InventoryDbContext> unitOfWork)
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100)
            .MustAsync(async (cmd, name, ct) => !await unitOfWork.Repository<SysRole>()
                .AnyAsync(r => r.Name == name.Trim() && r.Id != (cmd.Id ?? Guid.Empty), ct))
            .WithMessage("Tên role đã tồn tại.");
        RuleFor(x => x.Description).MaximumLength(255);
        RuleFor(x => x.Activities).NotNull().SetValidator(new ActivityPermissionInputsValidator(unitOfWork));
    }
}

public sealed class SaveRoleCommandHandler(IUnitOfWork<InventoryDbContext> unitOfWork, IPermissionService permissionService)
    : IRequestHandler<SaveRoleCommand, RoleDetailDto>
{
    public async Task<RoleDetailDto> Handle(SaveRoleCommand request, CancellationToken ct)
    {
        var roles = unitOfWork.Repository<SysRole>();
        SysRole role;
        List<SysRoleActivity> existing = [];

        if (request.Id is { } id)
        {
            role = await roles.FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException("Role", id);
            if (role.IsAdmin) throw new ConflictException("Không thể sửa role Admin (toàn quyền).");
            existing = await unitOfWork.Repository<SysRoleActivity>().Where(ra => ra.RoleId == id).ToListAsync(ct);
        }
        else
        {
            role = new SysRole { Name = request.Name.Trim() };
            roles.Add(role);
        }

        role.Name = request.Name.Trim();
        role.Description = request.Description?.Trim();
        PermissionMatrix.Apply(existing, request.Activities,
            activityId => new SysRoleActivity { RoleId = role.Id, ActivityId = activityId },
            unitOfWork.Repository<SysRoleActivity>());

        await unitOfWork.SaveChangesAsync(ct);
        permissionService.InvalidateAll(); // quyền role đổi → ảnh hưởng mọi user có role này

        return await RoleDetailReader.ReadAsync(unitOfWork, role.Id, ct);
    }
}

public sealed record DeleteRoleCommand(Guid Id) : IRequest;

public sealed class DeleteRoleCommandHandler(IUnitOfWork<InventoryDbContext> unitOfWork, IPermissionService permissionService)
    : IRequestHandler<DeleteRoleCommand>
{
    public async Task Handle(DeleteRoleCommand request, CancellationToken ct)
    {
        var role = await unitOfWork.Repository<SysRole>().FirstOrDefaultAsync(r => r.Id == request.Id, ct)
                   ?? throw new NotFoundException("Role", request.Id);
        if (role.IsAdmin) throw new ConflictException("Không thể xóa role Admin.");

        // Xóa mềm role + các liên kết của nó.
        unitOfWork.Repository<SysUserRole>().RemoveRange(
            await unitOfWork.Repository<SysUserRole>().Where(ur => ur.RoleId == role.Id).ToListAsync(ct));
        unitOfWork.Repository<SysRoleActivity>().RemoveRange(
            await unitOfWork.Repository<SysRoleActivity>().Where(ra => ra.RoleId == role.Id).ToListAsync(ct));
        unitOfWork.Repository<SysRole>().Remove(role);

        await unitOfWork.SaveChangesAsync(ct);
        permissionService.InvalidateAll();
    }
}
