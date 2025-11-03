using Dapper;
using Microsoft.Extensions.Configuration;
using Shared.Models.Database.Achievements;

namespace Repository;

public class AchievementsRepository : RepositoryBase
{
    public AchievementsRepository(IConfiguration configuration) : base(configuration)
    {
    }

    public async Task<IEnumerable<Achievement>?> GetAllAsync() =>
        await ExecuteSafelyAsync(Connection.QueryAsync<Achievement>(
            "SELECT * FROM achievements"));
}