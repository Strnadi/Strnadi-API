using Microsoft.Extensions.Logging;
using Tenant.Domain.Entities;
using Tenant.Domain.Exceptions;
using Tenant.Domain.Persistence;
using Tenant.Domain.Persistence.Repositories;

namespace Tenant.Application.Devices;

public class DevicesService(IDevicesRepository devices, IUnitOfWork unitOfWork, ILogger<DevicesService> logger)
{
    public async Task AddAsync(AddDeviceRequest request, Guid callerId, CancellationToken cancellationToken = default)
    {
        if (callerId != request.UserId)
            throw new ForbiddenException("You can only register a device for yourself");

        var existing = await devices.GetByFcmTokenAsync(request.FcmToken, cancellationToken);
        if (existing is not null)
        {
            // Same physical token re-registering (e.g. reinstall/relogin) - reassign instead of duplicating.
            existing.UserId = request.UserId;
            existing.DevicePlatform = request.DevicePlatform;
            existing.DeviceModel = request.DeviceModel;
        }
        else
        {
            devices.Add(new Device
            {
                UserId = request.UserId,
                FcmToken = request.FcmToken,
                DevicePlatform = request.DevicePlatform,
                DeviceModel = request.DeviceModel,
            });
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Device registered for user {UserId} ({Platform})", request.UserId, request.DevicePlatform);
    }

    public async Task UpdateAsync(UpdateDeviceRequest request, Guid callerId, CancellationToken cancellationToken = default)
    {
        var device = await devices.GetByFcmTokenAsync(request.OldFcmToken, cancellationToken)
            ?? throw new NotFoundException(nameof(Device), request.OldFcmToken);

        if (device.UserId != callerId)
            throw new ForbiddenException("You can only update your own device");

        device.FcmToken = request.NewFcmToken;
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string fcmToken, Guid callerId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var device = await devices.GetByFcmTokenAsync(fcmToken, cancellationToken)
            ?? throw new NotFoundException(nameof(Device), fcmToken);

        if (!isAdmin && device.UserId != callerId)
            throw new ForbiddenException("You can only delete your own device");

        devices.Remove(device);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Device removed for user {UserId}", device.UserId);
    }
}
