using FluentValidation;
using Inventory.Application.Common.Security;
using Inventory.Application.Features.V1.Auth.Services;
using Inventory.Domain.Entities.Sys;

namespace Inventory.Application.Features.V1.Auth.Commands.ResetPassword;

public sealed record ResetPasswordCommand(string Email, string Token, string NewPassword) : IRequest;

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.NewPassword).StrongPassword();
    }
}

public sealed class ResetPasswordCommandHandler(
    IUnitOfWork<InventoryDbContext> unitOfWork,
    IPasswordHasher hasher,
    TimeProvider clock) : IRequestHandler<ResetPasswordCommand>
{
    public async Task Handle(ResetPasswordCommand request, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var normalized = SysAccount.NormalizeEmail(request.Email);
        var user = await unitOfWork.Repository<SysAccount>().FirstOrDefaultAsync(u => u.Email == normalized, ct);

        if (user is null || !user.IsPasswordResetTokenValid(TokenHasher.Hash(request.Token), now))
            throw new ValidationException("token", "Link đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.");

        user.SetPasswordHash(hasher.Hash(user, request.NewPassword));

        // Đổi mật khẩu → đăng xuất mọi phiên.
        var sessions = await unitOfWork.Repository<SysRefreshToken>()
            .Where(t => t.UserId == user.Id && t.RevokedAt == null).ToListAsync(ct);
        sessions.ForEach(t => t.Revoke(now));

        await unitOfWork.SaveChangesAsync(ct);
    }
}
