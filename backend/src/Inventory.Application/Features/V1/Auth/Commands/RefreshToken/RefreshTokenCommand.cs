using FluentValidation;
using Inventory.Application.Common.Security;
using Inventory.Application.Features.V1.Auth.DTOs;
using Inventory.Application.Features.V1.Auth.Services;
using Inventory.Domain.Entities.Sys;
using Microsoft.Extensions.Logging;

namespace Inventory.Application.Features.V1.Auth.Commands.RefreshToken;

public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<AuthResponse>;

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}

/// <summary>
/// Rotation: mỗi lần refresh, token cũ bị revoke và thay bằng token mới.
/// Token đã revoke bị dùng lại (dấu hiệu bị đánh cắp) → revoke toàn bộ phiên của user.
/// </summary>
public sealed class RefreshTokenCommandHandler(
    IUnitOfWork<InventoryDbContext> unitOfWork,
    IAuthTokenIssuer tokenIssuer,
    ICurrentUserDtoFactory userDtoFactory,
    TimeProvider clock,
    ILogger<RefreshTokenCommandHandler> logger) : IRequestHandler<RefreshTokenCommand, AuthResponse>
{
    public async Task<AuthResponse> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var hash = TokenHasher.Hash(request.RefreshToken);
        var tokens = unitOfWork.Repository<SysRefreshToken>();

        var stored = await tokens.Include(t => t.User).FirstOrDefaultAsync(t => t.TokenHash == hash, ct)
                     ?? throw new UnauthorizedException("Refresh token không hợp lệ.");

        if (stored.RevokedAt is not null)
        {
            logger.LogWarning("Refresh token reuse detected for user {UserId}; revoking all sessions", stored.UserId);
            var active = await tokens.Where(t => t.UserId == stored.UserId && t.RevokedAt == null).ToListAsync(ct);
            active.ForEach(t => t.Revoke(now));
            await unitOfWork.SaveChangesAsync(ct);
            throw new UnauthorizedException("Refresh token đã bị thu hồi.");
        }

        if (!stored.IsActive(now) || !stored.User.IsActive)
            throw new UnauthorizedException("Refresh token đã hết hạn.");

        var issued = tokenIssuer.Issue(stored.User);
        stored.Revoke(now, issued.Entity.TokenHash);
        await unitOfWork.SaveChangesAsync(ct);

        return await userDtoFactory.CreateAuthResponseAsync(stored.User, issued, ct);
    }
}
