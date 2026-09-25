using System.Net;
using FluentValidation;
using Inventory.Application.Common.Security;
using Inventory.Application.Common.Models;
using Inventory.Domain.Entities.Sys;
using Microsoft.Extensions.Options;

namespace Inventory.Application.Features.V1.Auth.Commands.ForgotPassword;

public sealed record ForgotPasswordCommand(string Email) : IRequest;

public sealed class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator() => RuleFor(x => x.Email).NotEmpty().EmailAddress();
}

public sealed class ForgotPasswordCommandHandler(
    IUnitOfWork<InventoryDbContext> unitOfWork,
    IJwtTokenService jwt,
    IEmailSender email,
    IOptions<AppOptions> options,
    TimeProvider clock) : IRequestHandler<ForgotPasswordCommand>
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(30);

    public async Task Handle(ForgotPasswordCommand request, CancellationToken ct)
    {
        var normalized = SysAccount.NormalizeEmail(request.Email);
        var user = await unitOfWork.Repository<SysAccount>().FirstOrDefaultAsync(u => u.Email == normalized, ct);
        if (user is null || !user.IsActive) return; // không tiết lộ email có tồn tại hay không

        var token = jwt.GenerateSecureToken();
        user.PasswordResetTokenHash = TokenHasher.Hash(token);
        user.PasswordResetTokenExpiresAt = clock.GetUtcNow().UtcDateTime + TokenLifetime;
        await unitOfWork.SaveChangesAsync(ct);

        var link = $"{options.Value.FrontendBaseUrl.TrimEnd('/')}/reset-password" +
                   $"?email={WebUtility.UrlEncode(user.Email)}&token={WebUtility.UrlEncode(token)}";
        await email.SendAsync(user.Email, "Đặt lại mật khẩu Inventory",
            $"<p>Xin chào {WebUtility.HtmlEncode(user.FullName)},</p>" +
            $"<p>Nhấn vào <a href=\"{link}\">đây</a> để đặt lại mật khẩu (hiệu lực 30 phút).</p>", ct);
    }
}
