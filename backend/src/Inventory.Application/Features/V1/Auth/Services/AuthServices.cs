using FluentValidation;
using Inventory.Application.Common.Security;
using Inventory.Application.Features.V1.Auth.DTOs;
using Inventory.Domain.Entities.Sys;

namespace Inventory.Application.Features.V1.Auth.Services;

public sealed record IssuedTokens(string AccessToken, string RefreshToken, DateTime AccessTokenExpiresAt, SysRefreshToken Entity);

public interface IAuthTokenIssuer
{
    /// <summary>Tạo access + refresh token. Refresh token được Add vào UnitOfWork — caller SaveChanges.</summary>
    IssuedTokens Issue(SysAccount user);
}

public sealed class AuthTokenIssuer(IJwtTokenService jwt, IUnitOfWork<InventoryDbContext> unitOfWork, TimeProvider clock)
    : IAuthTokenIssuer
{
    public IssuedTokens Issue(SysAccount user)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var access = jwt.CreateAccessToken(user);
        var rawRefresh = jwt.GenerateSecureToken();

        var entity = new SysRefreshToken
        {
            UserId = user.Id,
            TokenHash = TokenHasher.Hash(rawRefresh),
            ExpiresAt = now + jwt.RefreshTokenLifetime
        };
        unitOfWork.Repository<SysRefreshToken>().Add(entity);

        return new IssuedTokens(access.Token, rawRefresh, access.ExpiresAt, entity);
    }
}

public interface ICurrentUserDtoFactory
{
    Task<CurrentUserDto> CreateAsync(SysAccount user, CancellationToken ct);

    Task<AuthResponse> CreateAuthResponseAsync(SysAccount user, IssuedTokens tokens, CancellationToken ct);
}

public sealed class CurrentUserDtoFactory(IPermissionService permissions) : ICurrentUserDtoFactory
{
    public async Task<CurrentUserDto> CreateAsync(SysAccount user, CancellationToken ct)
    {
        var effective = await permissions.GetEffectiveAsync(user.Id, ct);
        var items = effective.Activities
            .Where(a => a.Value.Any)
            .OrderBy(a => a.Key)
            .Select(a => new PermissionDto(a.Key, a.Value.C, a.Value.R, a.Value.U, a.Value.D))
            .ToList();
        return new CurrentUserDto(user.Id, user.Email, user.FullName, effective.IsAdmin, effective.Roles, items);
    }

    public async Task<AuthResponse> CreateAuthResponseAsync(SysAccount user, IssuedTokens tokens, CancellationToken ct) =>
        new(tokens.AccessToken, tokens.RefreshToken, tokens.AccessTokenExpiresAt, await CreateAsync(user, ct));
}

public static class PasswordRules
{
    public static IRuleBuilderOptions<T, string> StrongPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Mật khẩu là bắt buộc.")
            .MinimumLength(8).WithMessage("Mật khẩu tối thiểu 8 ký tự.")
            .MaximumLength(100)
            .Matches("[A-Z]").WithMessage("Mật khẩu phải có ít nhất 1 chữ hoa.")
            .Matches("[a-z]").WithMessage("Mật khẩu phải có ít nhất 1 chữ thường.")
            .Matches("[0-9]").WithMessage("Mật khẩu phải có ít nhất 1 chữ số.");
}
