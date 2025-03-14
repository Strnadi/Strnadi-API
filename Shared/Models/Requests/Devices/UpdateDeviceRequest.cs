namespace Shared.Models.Requests.Devices;

public class UpdateDeviceRequest
{
    public string OldFcmToken { get; set; }
    public string NewFcmToken { get; set; }
}