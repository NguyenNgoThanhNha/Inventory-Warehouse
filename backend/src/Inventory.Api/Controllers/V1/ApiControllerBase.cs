using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers.V1;

/// <summary>Controller mỏng: chỉ nhận request và gọi Mediator (chuẩn BE §6). Mặc định yêu cầu đăng nhập.</summary>
[ApiController]
[Authorize]
[Produces("application/json")]
public abstract class ApiControllerBase(ISender mediator) : ControllerBase
{
    protected ISender Mediator { get; } = mediator;
}
