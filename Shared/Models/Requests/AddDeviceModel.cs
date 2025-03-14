namespace Shared.Models.Requests;

public class AddDeviceModel
{
    public string UserEmail { get; set; }
    
    public string FcmToken { get; set; }

    public string DevicePlatform { get; set; }
    
    public string DeviceName { get; set; }
}