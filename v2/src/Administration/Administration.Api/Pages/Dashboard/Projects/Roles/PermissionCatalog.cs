using System.Reflection;
using Platform.Shared.Kernel.Authorization;

namespace Administration.Api.Pages.Dashboard.Projects.Roles;

/// <summary>All grantable permissions, read off the Permissions constants so the role forms stay in sync automatically.</summary>
public static class PermissionCatalog
{
    public static readonly IReadOnlyList<string> All = typeof(Permissions)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.FieldType == typeof(string))
        .Select(f => (string)f.GetValue(null)!)
        .OrderBy(p => p, StringComparer.Ordinal)
        .ToList();
}
