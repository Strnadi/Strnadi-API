namespace Shared.Models.Requests.Devices;

public class AddDeviceRequest
{
    public string UserEmail { get; set; }
    
    public string FcmToken { get; set; }

    public string DevicePlatform { get; set; }
    
    public string DeviceName { get; set; }
}