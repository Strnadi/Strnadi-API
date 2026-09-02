using Tenant.Domain.Entities;

namespace Tenant.Domain.Persistence.Repositories;

public interface IDevicesRepository
{
    Task<Device?> GetByFcmTokenAsync(string fcmToken, CancellationToken cancellationToken = default);

    Task<Device[]> GetAllByUserIdAsync(int userId, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string fcmToken, CancellationToken cancellationToken = default);

    void Add(Device device);

    void Remove(Device device);
}
