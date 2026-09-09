using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Mvc;

namespace Tenant.Api.Extensions;

public static class ControllerBaseExtensions
{
    extension(ControllerBase controller)
    {
        public Guid GetCallerId() =>
            Guid.Parse(controller.User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

        public Guid? GetCallerIdOrDefault() =>
            controller.User.Identity?.IsAuthenticated == true ? controller.GetCallerId() : null;

        public bool IsAdmin() =>
            controller.User.HasClaim("role", "admin");
    }
}
