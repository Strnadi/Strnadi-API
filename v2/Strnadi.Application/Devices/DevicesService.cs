using Strnadi.Domain.Entities;
using Strnadi.Domain.Exceptions;
using Strnadi.Domain.Persistence;
using Strnadi.Domain.Persistence.Repositories;

namespace Strnadi.Application.Devices;

public class DevicesService(IDevicesRepository devices, IUnitOfWork unitOfWork)
{
    public async Task AddAsync(AddDeviceRequest request, int callerId, CancellationToken cancellationToken = default)
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
    }

    public async Task UpdateAsync(UpdateDeviceRequest request, int callerId, CancellationToken cancellationToken = default)
    {
        var device = await devices.GetByFcmTokenAsync(request.OldFcmToken, cancellationToken)
            ?? throw new NotFoundException(nameof(Device), request.OldFcmToken);

        if (device.UserId != callerId)
            throw new ForbiddenException("You can only update your own device");

        device.FcmToken = request.NewFcmToken;
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string fcmToken, int callerId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var device = await devices.GetByFcmTokenAsync(fcmToken, cancellationToken)
            ?? throw new NotFoundException(nameof(Device), fcmToken);

        if (!isAdmin && device.UserId != callerId)
            throw new ForbiddenException("You can only delete your own device");

        devices.Remove(device);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
