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
using Shared.Models.Database;
using Shared.Models.Requests.Devices;

namespace Repository;

/// <summary>
/// Provides database operations for registered devices and FCM tokens.
/// </summary>
public class DevicesRepository : RepositoryBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DevicesRepository"/> class.
    /// </summary>
    /// <param name="configuration">Application configuration used by the repository base.</param>
    public DevicesRepository(IConfiguration configuration) : base(configuration)
    {
    }

    /// <summary>
    /// Checks whether a device exists for the specified FCM token.
    /// </summary>
    /// <param name="fcmToken">FCM token to look up.</param>
    /// <returns><c>true</c> when a matching device exists; otherwise, <c>false</c>.</returns>
    public async Task<bool> ExistsAsync(string fcmToken) =>
        await ExecuteSafelyAsync(async () =>
        {
            const string sql = "SELECT COUNT(*) FROM devices WHERE fcm_token = @FcmToken";
            return await Connection.ExecuteScalarAsync<int>(sql, new { FcmToken = fcmToken }) != 0;
        });
    
    /// <summary>
    /// Inserts a new device record.
    /// </summary>
    /// <param name="request">Device data to insert.</param>
    /// <returns><c>true</c> when the insert affects a row; otherwise, <c>false</c>.</returns>
    public async Task<bool> AddAsync(AddDeviceRequest request) =>
        await ExecuteSafelyAsync(async () =>
        {
            const string sql = """
                               INSERT INTO devices(user_id, fcm_token, device_platform, device_model)
                               VALUES(@UserId, @FcmToken, @DevicePlatform, @DeviceModel)
                               """;

            return await Connection.ExecuteAsync(sql, new
            {
                request.UserId,
                request.FcmToken,
                request.DevicePlatform,
                request.DeviceModel
            }) != 0;
        });

    /// <summary>
    /// Replaces an existing device FCM token.
    /// </summary>
    /// <param name="request">The old and new token values.</param>
    /// <returns><c>true</c> when the token is updated; otherwise, <c>false</c>.</returns>
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

    /// <summary>
    /// Deletes a device by FCM token.
    /// </summary>
    /// <param name="fcmToken">FCM token identifying the device to delete.</param>
    /// <returns><c>true</c> when the device is deleted; otherwise, <c>false</c>.</returns>
    public async Task<bool> DeleteAsync(string fcmToken) =>
        await ExecuteSafelyAsync(async () =>
        {
            if (!await ExistsAsync(fcmToken))
                return false;

            const string sql = "DELETE FROM devices WHERE fcm_token = @FcmToken";

            return await Connection.ExecuteAsync(sql, new { FcmToken = fcmToken }) != 0;
        });

    /// <summary>
    /// Assigns an existing device token to another user.
    /// </summary>
    /// <param name="newUserId">Identifier of the user who should own the device.</param>
    /// <param name="oldFcmToken">Existing FCM token to update.</param>
    /// <returns><c>true</c> when the owner is updated; otherwise, <c>false</c>.</returns>
    public async Task<bool> ChangeUserAsync(int newUserId, string oldFcmToken) =>
        await ExecuteSafelyAsync(async () =>
        {
            if (!await ExistsAsync(oldFcmToken))
                return false;
            
            const string sql = "UPDATE devices SET user_id = @NewUserId WHERE fcm_token = @OldFcmToken;";

            return await Connection.ExecuteAsync(sql,
                       new
                       {
                           NewUserId = newUserId,
                           OldFcmToken = oldFcmToken
                       }) != 0;
        });

    /// <summary>
    /// Gets all devices registered to a user.
    /// </summary>
    /// <param name="userId">User identifier to query.</param>
    /// <returns>The user's devices, or <c>null</c> when the query fails.</returns>
    public async Task<IEnumerable<Device>?> GetAllByUserIdAsync(int userId) =>
        await ExecuteSafelyAsync(Connection.QueryAsync<Device>(
            "SELECT * FROM devices WHERE user_id = @UserId",
            new { UserId = userId }
        ));

}
