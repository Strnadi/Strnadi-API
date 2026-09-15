namespace Administration.Domain.Persistence.Repositories;

public interface IUserPermissionsRepository
{
    Task<bool> HasPermissionAsync(Guid userId, string permission);

    /// <summary>
    /// Whether the user has an outstanding (unaccepted) document that is currently in effect.
    /// A global document (ProjectId null) blocks regardless of project; a project-scoped document
    /// only blocks for that project. Users with outstanding consent must be treated as having no roles.
    /// </summary>
    Task<bool> HasOutstandingConsentAsync(Guid userId, Guid? projectId);
}
