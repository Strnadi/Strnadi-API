namespace Tenant.Application.Devices;

public record AddDeviceRequest(Guid UserId, string FcmToken, string DevicePlatform, string DeviceModel);
