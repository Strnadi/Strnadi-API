namespace Administration.Domain.Persistence.Repositories;

public interface IUserPermissionsRepository
{
    Task<bool> HasPermissionAsync(Guid userId, string permission);
}
