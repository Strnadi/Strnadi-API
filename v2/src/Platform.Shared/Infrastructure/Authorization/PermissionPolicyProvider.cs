using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Platform.Shared.Infrastructure.Authorization;

public class PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith("Permission:"))
            return await base.GetPolicyAsync(policyName);

        var permission = policyName["Permission:".Length..];
        return new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(permission))
            .Build();
    }
}
