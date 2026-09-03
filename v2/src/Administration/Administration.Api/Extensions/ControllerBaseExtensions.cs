using Administration.Api.Auth;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;

namespace Administration.Api.Extensions;

public static class ControllerBaseExtensions
{
    extension(ControllerBase controller)
    {
        public int GetCallerId() =>
            int.Parse(controller.User.FindFirst(OpenIddictConstants.Claims.Subject)!.Value);

        public int? GetCallerIdOrDefault() =>
            controller.User.Identity?.IsAuthenticated == true ? controller.GetCallerId() : null;

        public bool HasPermission(string code) =>
            controller.User.HasClaim(AuthConstants.PermissionClaimType, code);
    }
}