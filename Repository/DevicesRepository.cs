using Dapper;
using Microsoft.Extensions.Configuration;
using Shared.Models.Requests;

namespace Repository;

public class DevicesRepository : RepositoryBase
{
    public DevicesRepository(IConfiguration configuration) : base(configuration)
    {
    }
    
    public async Task<bool> AddAsync(AddDeviceModel model)
    {
        return await ExecuteSafelyAsync(async () =>
        {
            const string sql = """
                               INSERT INTO devices(user_email, fcm_token, device_platform, device_name)
                               VALUES(@UserEmail, @FcmToken, @DevicePlatform, @DeviceName)
                               """;

            return await Connection.ExecuteAsync(sql, new
            {
                model.UserEmail,
                model.FcmToken,
                model.DevicePlatform,
                model.DeviceName
            }) != 0;
        });
    }
}