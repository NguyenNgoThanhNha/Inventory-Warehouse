using Inventory.Application.Features.V1.Auth.DTOs;
using Inventory.Application.Features.V1.Auth.Services;
using Inventory.Domain.Entities.Sys;

namespace Inventory.Application.Features.V1.Auth.Queries.GetCurrentUser;

public sealed record GetCurrentUserQuery : IRequest<CurrentUserDto>;

public sealed class GetCurrentUserQueryHandler(
    IUnitOfWork<InventoryDbContext> unitOfWork,
    ICurrentUser currentUser,
    ICurrentUserDtoFactory userDtoFactory) : IRequestHandler<GetCurrentUserQuery, CurrentUserDto>
{
    public async Task<CurrentUserDto> Handle(GetCurrentUserQuery request, CancellationToken ct)
    {
        var userId = currentUser.UserId;
        var user = await unitOfWork.Repository<SysAccount>().AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct)
                   ?? throw new NotFoundException("User", userId);
        return await userDtoFactory.CreateAsync(user, ct);
    }
}
