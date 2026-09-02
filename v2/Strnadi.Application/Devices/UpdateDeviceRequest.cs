namespace Strnadi.Application.Devices;

public record UpdateDeviceRequest(string OldFcmToken, string NewFcmToken);
