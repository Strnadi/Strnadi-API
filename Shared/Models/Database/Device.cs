namespace Shared.Models.Database;

public class Device
{
    public int Id { get; set; }
    
    public string FcmToken { get; set; }
    
    public string DevicePlatform { get; set; }
    
    public string DeviceModel { get; set; }
    
    public int UserId { get; set; }
}