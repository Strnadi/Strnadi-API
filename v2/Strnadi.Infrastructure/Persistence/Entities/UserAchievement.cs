using System;
using System.Collections.Generic;

namespace Strnadi.Infrastructure.Persistence.Entities;

public partial class UserAchievement
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int AchievementId { get; set; }

    public virtual Achievement Achievement { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
