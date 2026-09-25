namespace Inventory.Application.Features.V1.Roles.DTOs;

public sealed record ActivityDto(Guid Id, string Code, string Name, string? Description);

public sealed record ActivityPermissionInput(Guid ActivityId, bool C, bool R, bool U, bool D)
{
    public bool Any => C || R || U || D;
}

public sealed record ActivityPermissionDto(Guid ActivityId, string Code, string Name, bool C, bool R, bool U, bool D);

public sealed record RoleRefDto(Guid Id, string Name);

public sealed record RoleDto(Guid Id, string Name, string? Description, bool IsAdmin, int UserCount);

public sealed record RoleDetailDto(
    Guid Id, string Name, string? Description, bool IsAdmin, int UserCount, IReadOnlyList<ActivityPermissionDto> Activities);
