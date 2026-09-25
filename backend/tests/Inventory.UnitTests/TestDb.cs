using Inventory.Application.Common.Interfaces;
using Inventory.Domain.Constants;
using Inventory.Domain.Entities.Sys;
using Inventory.Infrastructure.Commons;
using Inventory.Persistence;
using Inventory.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Inventory.UnitTests;

/// <summary>DbContext InMemory + UnitOfWork thật + audit interceptor — để test handler/service đúng như chạy thật.</summary>
public sealed class TestDb : IDisposable
{
    public TestDb()
    {
        var audit = Substitute.For<IAuditUser>();
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new AuditSaveChangesInterceptor(audit, TimeProvider.System))
            .Options;
        Context = new InventoryDbContext(options);
        UnitOfWork = new UnitOfWork<InventoryDbContext>(Context, new ServiceCollection().BuildServiceProvider());
    }

    public InventoryDbContext Context { get; }
    public IUnitOfWork<InventoryDbContext> UnitOfWork { get; }

    /// <summary>Seed toàn bộ ConstActivity.All; trả về map code → activity.</summary>
    public Dictionary<string, SysActivity> SeedActivities()
    {
        var map = ConstActivity.All.ToDictionary(a => a.Code, a => new SysActivity { Code = a.Code, Name = a.Name });
        Context.SysActivities.AddRange(map.Values);
        Context.SaveChanges();
        return map;
    }

    public SysAccount AddUser(string email, bool active = true, params SysRole[] roles)
    {
        var user = new SysAccount { Email = email, FullName = email, IsActive = active };
        Context.SysAccounts.Add(user);
        foreach (var role in roles) Context.SysUserRoles.Add(new SysUserRole { UserId = user.Id, RoleId = role.Id });
        Context.SaveChanges();
        return user;
    }

    public SysRole AddRole(string name, int? roleType = null, params (SysActivity Activity, string Flags)[] permissions)
    {
        var role = new SysRole { Name = name, RoleType = roleType };
        Context.SysRoles.Add(role);
        foreach (var (activity, flags) in permissions)
        {
            var ra = new SysRoleActivity { RoleId = role.Id, ActivityId = activity.Id };
            ra.SetFlags(flags.Contains('C'), flags.Contains('R'), flags.Contains('U'), flags.Contains('D'));
            Context.SysRoleActivities.Add(ra);
        }
        Context.SaveChanges();
        return role;
    }

    public void Dispose() => Context.Dispose();
}
