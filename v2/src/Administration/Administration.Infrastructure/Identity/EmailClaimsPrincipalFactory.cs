using System.Security.Claims;
using Administration.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Administration.Infrastructure.Identity;

public class EmailClaimsPrincipalFactory(
    UserManager<User> userManager,
    RoleManager<Role> roleManager,
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<User, Role>(userManager, roleManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(User user)
    {
        var userId = await UserManager.GetUserIdAsync(user);
        var email = await UserManager.GetEmailAsync(user);
        var userName = await UserManager.GetUserNameAsync(user) ?? email;

        var id = new ClaimsIdentity("Identity.Application",
            Options.ClaimsIdentity.UserNameClaimType,
            Options.ClaimsIdentity.RoleClaimType);
        id.AddClaim(new Claim(Options.ClaimsIdentity.UserIdClaimType, userId));
        id.AddClaim(new Claim(Options.ClaimsIdentity.UserNameClaimType, userName!));

        if (!string.IsNullOrEmpty(email))
            id.AddClaim(new Claim(Options.ClaimsIdentity.EmailClaimType, email));

        if (UserManager.SupportsUserSecurityStamp)
            id.AddClaim(new Claim(Options.ClaimsIdentity.SecurityStampClaimType, await UserManager.GetSecurityStampAsync(user)));

        if (UserManager.SupportsUserClaim)
            id.AddClaims(await UserManager.GetClaimsAsync(user));

        if (UserManager.SupportsUserRole)
        {
            var roles = await UserManager.GetRolesAsync(user);
            foreach (var roleName in roles)
            {
                id.AddClaim(new Claim(Options.ClaimsIdentity.RoleClaimType, roleName));
                if (RoleManager.SupportsRoleClaims)
                {
                    var role = await RoleManager.FindByNameAsync(roleName);
                    if (role != null)
                        id.AddClaims(await RoleManager.GetClaimsAsync(role));
                }
            }
        }

        return id;
    }
}
