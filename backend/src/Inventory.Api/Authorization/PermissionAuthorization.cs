using Inventory.Application.Common.Interfaces;
using Inventory.Application.Common.Security;
using Inventory.Domain.Constants;
using Inventory.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Inventory.Api.Authorization;

/// <summary>
/// [HasPermission(ConstActivity.Ticket, ActivityType.Create)] hoặc [HasPermission("USER:R|ROLE:R")] (OR).
/// Sinh policy "PERM:..." được PermissionPolicyProvider dựng động.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string activityCode, ActivityType type)
        : base(ConstPolicy.PermissionPrefix + PermissionKey.Format(activityCode, type)) { }

    public HasPermissionAttribute(string anyOfPermissionKeys)
        : base(ConstPolicy.PermissionPrefix + anyOfPermissionKeys) { }
}

public sealed class PermissionRequirement(IReadOnlyList<(string Code, ActivityType Type)> anyOf) : IAuthorizationRequirement
{
    public IReadOnlyList<(string Code, ActivityType Type)> AnyOf { get; } = anyOf;
}

public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(ConstPolicy.PermissionPrefix, StringComparison.Ordinal))
            return await base.GetPolicyAsync(policyName);

        var keys = policyName[ConstPolicy.PermissionPrefix.Length..]
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(k => PermissionKey.TryParse(k, out var code, out var type)
                ? (code, type)
                : throw new InvalidOperationException($"Permission key không hợp lệ: '{k}' (dạng CODE:C|R|U|D)"))
            .ToList();

        return new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(keys))
            .Build();
    }
}

public sealed class PermissionAuthorizationHandler(ICurrentUser currentUser) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (!currentUser.IsAuthenticated) return;
        var permissions = await currentUser.GetPermissionsAsync();
        if (requirement.AnyOf.Any(p => permissions.Has(p.Code, p.Type))) context.Succeed(requirement);
    }
}
