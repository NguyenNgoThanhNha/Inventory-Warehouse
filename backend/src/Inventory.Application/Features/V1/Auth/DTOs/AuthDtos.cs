namespace Inventory.Application.Features.V1.Auth.DTOs;

public sealed record PermissionDto(string Code, bool C, bool R, bool U, bool D);

public sealed record CurrentUserDto(
    Guid Id,
    string Email,
    string FullName,
    bool IsAdmin,
    IReadOnlyList<string> Roles,
    IReadOnlyList<PermissionDto> Permissions);

public sealed record AuthResponse(string AccessToken, string RefreshToken, DateTime AccessTokenExpiresAt, CurrentUserDto User);
