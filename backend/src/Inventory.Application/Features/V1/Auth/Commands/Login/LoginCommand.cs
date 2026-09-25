using FluentValidation;
using Inventory.Application.Features.V1.Auth.DTOs;
using Inventory.Application.Features.V1.Auth.Services;
using Inventory.Domain.Entities.Sys;

namespace Inventory.Application.Features.V1.Auth.Commands.Login;

public sealed record LoginCommand(string Email, string Password) : IRequest<AuthResponse>;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public sealed class LoginCommandHandler(
    IUnitOfWork<InventoryDbContext> unitOfWork,
    IPasswordHasher hasher,
    IAuthTokenIssuer tokenIssuer,
    ICurrentUserDtoFactory userDtoFactory) : IRequestHandler<LoginCommand, AuthResponse>
{
    public async Task<AuthResponse> Handle(LoginCommand request, CancellationToken ct)
    {
        var email = SysAccount.NormalizeEmail(request.Email);
        var user = await unitOfWork.Repository<SysAccount>().FirstOrDefaultAsync(u => u.Email == email, ct);

        // Cùng một thông báo cho mọi trường hợp để tránh dò email.
        if (user is null || !user.IsActive || !hasher.Verify(user, request.Password))
            throw new UnauthorizedException(ConstMessage.InvalidCredentials);

        var tokens = tokenIssuer.Issue(user);
        await unitOfWork.SaveChangesAsync(ct);

        return await userDtoFactory.CreateAuthResponseAsync(user, tokens, ct);
    }
}
