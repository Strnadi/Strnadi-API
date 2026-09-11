using System.Security.Claims;

namespace Platform.Shared.Common.Extensions;

public static class ClaimsPrincipalExtensions
{
    extension(ClaimsPrincipal principal)
    {
        // A cookie-authenticated principal carries the id as ClaimTypes.NameIdentifier; a
        // Bearer-authenticated (OpenIddict/JWT) principal carries it unmapped as "sub" - check
        // both so either caller resolves correctly.
        public Guid GetUserId() =>
            Guid.Parse(principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

        public Guid? GetUserIdOrDefault() =>
            principal.Identity?.IsAuthenticated == true ? principal.GetUserId() : null;
    }
}
