namespace Tenant.Application.Devices;

public record UpdateDeviceRequest(string OldFcmToken, string NewFcmToken);
