namespace Tenant.Domain.Entities;

public partial class Device
{
    public int Id { get; set; }

    public string FcmToken { get; set; } = null!;

    public string DevicePlatform { get; set; } = null!;

    public string DeviceModel { get; set; } = null!;

    public Guid UserId { get; set; }
}
