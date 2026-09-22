namespace Administration.Domain.Persistence.Repositories;

public interface IUserPermissionsRepository
{
    /// <summary>
    /// Whether the user holds a role granting <paramref name="permission"/>.
    /// With <paramref name="projectId"/> null, only a global role (Role.ProjectId == null) counts -
    /// use this for platform-wide actions that have no single project to scope by.
    /// With <paramref name="projectId"/> given, a role scoped to that exact project OR a global role
    /// counts - a global role always acts as a wildcard for any project-scoped check.
    /// </summary>
    Task<bool> HasPermissionAsync(Guid userId, string permission, Guid? projectId = null);

    /// <summary>
    /// Whether the user has an outstanding (unaccepted) document that is currently in effect.
    /// A global document (ProjectId null) blocks regardless of project; a project-scoped document
    /// only blocks for that project. Users with outstanding consent must be treated as having no roles.
    /// </summary>
    Task<bool> HasOutstandingConsentAsync(Guid userId, Guid? projectId);
}
