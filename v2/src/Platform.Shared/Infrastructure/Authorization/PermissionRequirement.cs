using Microsoft.AspNetCore.Authorization;

namespace Platform.Shared.Infrastructure.Authorization;

public record PermissionRequirement(string Permission) : IAuthorizationRequirement;
