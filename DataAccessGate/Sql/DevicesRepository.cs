using Dapper;

namespace DataAccessGate.Sql;

public class DevicesRepository : RepositoryBase
{
    public DevicesRepository(IConfiguration configuration) : base(configuration)
    {
    }

    public void DeleteDevice(string requestOldToken)
    {
        const string sql = "DELETE FROM \"Devices\" WHERE \"FcmToken\" = @OldToken";
        Connection.Execute(sql, new { OldToken = requestOldToken });
    }

    public void AddDevice(int userId, string newToken, string? deviceName, string? devicePlatform)
    {
        const string sql = """
                           INSERT INTO "Devices"("DeviceName", "DevicePlatform", "FcmToken", "UserId")
                           VALUES (@DeviceName, @DevicePlatform, @NewToken, @UserId)
                           """;
        
        Connection.Execute(sql, new { DeviceName = deviceName, DevicePlatform = devicePlatform, NewToken = newToken, UserId = userId });
    }

    public void UpdateDevice(string oldToken, string newToken)
    {
        const string sql = """UPDATE "Devices" SET "FcmToken" = @NewToken WHERE "FcmToken" = @OldToken; """;
        Connection.Execute(sql, new { OldToken = oldToken, NewToken = newToken });
    }
}