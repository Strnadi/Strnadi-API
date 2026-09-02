namespace Strnadi.Application.Devices;

public record AddDeviceRequest(int UserId, string FcmToken, string DevicePlatform, string DeviceModel);
