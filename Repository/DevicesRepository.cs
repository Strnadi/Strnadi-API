using Dapper;
using Microsoft.Extensions.Configuration;
using Shared.Models.Requests.Devices;

namespace Repository;

public class DevicesRepository : RepositoryBase
{
    public DevicesRepository(IConfiguration configuration) : base(configuration)
    {
    }

    public async Task<bool> ExistsAsync(string fcmToken) =>
        await ExecuteSafelyAsync(async () =>
        {
            const string sql = "SELECT COUNT(*) FROM Devices WHERE fcm_token = @FcmToken";
            return await Connection.ExecuteScalarAsync<int>(sql, new { FcmToken = fcmToken }) != 0;
        });
    
    public async Task<bool> AddAsync(AddDeviceRequest request) =>
        await ExecuteSafelyAsync(async () =>
        {
            const string sql = """
                               INSERT INTO devices(user_email, fcm_token, device_platform, device_name)
                               VALUES(@UserEmail, @FcmToken, @DevicePlatform, @DeviceName)
                               """;

            return await Connection.ExecuteAsync(sql, new
            {
                request.UserEmail,
                request.FcmToken,
                request.DevicePlatform,
                request.DeviceName
            }) != 0;
        });

    public async Task<bool> UpdateAsync(UpdateDeviceRequest request) =>
        await ExecuteSafelyAsync(async () =>
        {
            if (!await ExistsAsync(request.OldFcmToken))
                            return false;
            
            const string sql = "UPDATE devices SET fcm_token = @NewFcmToken WHERE fcm_token = @OldFcmToken";

            return await Connection.ExecuteAsync(sql, new
            {
                request.NewFcmToken, 
                request.OldFcmToken
            }) != 0;
        });

    public async Task<bool> DeleteAsync(string fcmToken) =>
        await ExecuteSafelyAsync(async () =>
        {
            if (!await ExistsAsync(fcmToken))
                return false;

            const string sql = "DELETE FROM Devices WHERE fcm_token = @FcmToken";

            return await Connection.ExecuteAsync(sql, new { FcmToken = fcmToken }) != 0;
        });
}