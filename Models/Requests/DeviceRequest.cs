namespace Models.Requests;

public class DeviceRequest
{
    public string? OldToken { get; set; }
    
    public string? NewToken { get; set; }

    public string? DeviceName { get; set; }

    public string? DevicePlatform { get; set; }
}