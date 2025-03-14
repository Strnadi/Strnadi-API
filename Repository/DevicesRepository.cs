/*
 * Copyright (C) 2024 Stanislav Motsnyi
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
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
            const string sql = "SELECT COUNT(*) FROM devices WHERE fcm_token = @FcmToken";
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