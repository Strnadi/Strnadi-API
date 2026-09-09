using Tenant.Domain.Configuration;

namespace Tenant.Application.Common;

public class LinkBuilder(IHostSettings hostSettings)
{
    public string AchievementImageLink(int achievementId) =>
        $"{hostSettings.ApiHost}/achievements/{achievementId}/photo";
}
