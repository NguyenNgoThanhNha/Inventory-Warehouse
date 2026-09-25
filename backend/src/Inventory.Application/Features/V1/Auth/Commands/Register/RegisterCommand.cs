using FluentValidation;
using Inventory.Application.Features.V1.Auth.DTOs;
using Inventory.Application.Features.V1.Auth.Services;
using Inventory.Domain.Entities.Sys;

namespace Inventory.Application.Features.V1.Auth.Commands.Register;

public sealed record RegisterCommand(string Email, string Password, string FullName) : IRequest<AuthResponse>;

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Password).StrongPassword();
    }
}

public sealed class RegisterCommandHandler(
    IUnitOfWork<InventoryDbContext> unitOfWork,
    IPasswordHasher hasher,
    IAuthTokenIssuer tokenIssuer,
    ICurrentUserDtoFactory userDtoFactory) : IRequestHandler<RegisterCommand, AuthResponse>
{
    public async Task<AuthResponse> Handle(RegisterCommand request, CancellationToken ct)
    {
        var email = SysAccount.NormalizeEmail(request.Email);
        if (await unitOfWork.Repository<SysAccount>().AnyAsync(u => u.Email == email, ct))
            throw new ConflictException(ConstMessage.EmailExists);

        var defaultRole = await unitOfWork.Repository<SysRole>()
                              .FirstOrDefaultAsync(r => r.Name == ConstRole.DefaultForRegistration, ct)
                          ?? throw new NotFoundException("Role", ConstRole.DefaultForRegistration);

        var user = new SysAccount { Email = email, FullName = request.FullName.Trim() };
        user.SetPasswordHash(hasher.Hash(user, request.Password));
        user.UserRoles.Add(new SysUserRole { UserId = user.Id, RoleId = defaultRole.Id });
        unitOfWork.Repository<SysAccount>().Add(user);

        var tokens = tokenIssuer.Issue(user);
        await unitOfWork.SaveChangesAsync(ct);

        return await userDtoFactory.CreateAuthResponseAsync(user, tokens, ct);
    }
}
