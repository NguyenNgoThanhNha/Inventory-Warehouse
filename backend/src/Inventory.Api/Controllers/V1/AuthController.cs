using Inventory.Application.Features.V1.Auth.Commands.ForgotPassword;
using Inventory.Application.Features.V1.Auth.Commands.Login;
using Inventory.Application.Features.V1.Auth.Commands.Logout;
using Inventory.Application.Features.V1.Auth.Commands.RefreshToken;
using Inventory.Application.Features.V1.Auth.Commands.Register;
using Inventory.Application.Features.V1.Auth.Commands.ResetPassword;
using Inventory.Application.Features.V1.Auth.DTOs;
using Inventory.Application.Features.V1.Auth.Queries.GetCurrentUser;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Inventory.Api.Controllers.V1;

[Route("api/v1/auth")]
[EnableRateLimiting(RateLimitPolicies.Auth)]
public sealed class AuthController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpPost("register"), AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register(RegisterCommand command, CancellationToken ct) =>
        Ok(await Mediator.Send(command, ct));

    [HttpPost("login"), AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(LoginCommand command, CancellationToken ct) =>
        Ok(await Mediator.Send(command, ct));

    [HttpPost("refresh"), AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshTokenCommand command, CancellationToken ct) =>
        Ok(await Mediator.Send(command, ct));

    [HttpPost("logout"), AllowAnonymous]
    public async Task<IActionResult> Logout(LogoutCommand command, CancellationToken ct)
    {
        await Mediator.Send(command, ct);
        return NoContent();
    }

    [HttpPost("forgot-password"), AllowAnonymous]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordCommand command, CancellationToken ct)
    {
        await Mediator.Send(command, ct);
        return NoContent();
    }

    [HttpPost("reset-password"), AllowAnonymous]
    public async Task<IActionResult> ResetPassword(ResetPasswordCommand command, CancellationToken ct)
    {
        await Mediator.Send(command, ct);
        return NoContent();
    }

    [HttpGet("me")]
    [DisableRateLimiting]
    public async Task<ActionResult<CurrentUserDto>> Me(CancellationToken ct) => Ok(await Mediator.Send(new GetCurrentUserQuery(), ct));
}

public static class RateLimitPolicies
{
    public const string Auth = "auth";
}
