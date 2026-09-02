using Microsoft.EntityFrameworkCore;
using Tenant.Domain.Entities;
using Tenant.Domain.Persistence.Repositories;

namespace Tenant.Infrastructure.Persistence.Repositories;

public class DevicesRepository(AppDbContext db) : IDevicesRepository
{
    public Task<Device?> GetByFcmTokenAsync(string fcmToken, CancellationToken cancellationToken = default) =>
        db.Devices.FirstOrDefaultAsync(d => d.FcmToken == fcmToken, cancellationToken);

    public Task<Device[]> GetAllByUserIdAsync(int userId, CancellationToken cancellationToken = default) =>
        db.Devices.Where(d => d.UserId == userId).ToArrayAsync(cancellationToken);

    public Task<bool> ExistsAsync(string fcmToken, CancellationToken cancellationToken = default) =>
        db.Devices.AnyAsync(d => d.FcmToken == fcmToken, cancellationToken);

    public void Add(Device device) => db.Devices.Add(device);

    public void Remove(Device device) => db.Devices.Remove(device);
}
