using System;
using System.Collections.Generic;

namespace Strnadi.Infrastructure.Persistence.Entities;

public partial class Device
{
    public int Id { get; set; }

    public string FcmToken { get; set; } = null!;

    public string DevicePlatform { get; set; } = null!;

    public string DeviceModel { get; set; } = null!;

    public int UserId { get; set; }

    public virtual User User { get; set; } = null!;
}
