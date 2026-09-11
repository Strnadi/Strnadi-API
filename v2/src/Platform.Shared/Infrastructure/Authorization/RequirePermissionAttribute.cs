using Microsoft.AspNetCore.Authorization;

namespace Platform.Shared.Infrastructure.Authorization;

public class RequirePermissionAttribute(string permission) : AuthorizeAttribute(policy: $"Permission:{permission}");
