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
using Shared.Models.Database.Photos;
using Shared.Models.Requests.Photos;
using Shared.Tools;

namespace Repository;

public class PhotosRepository : RepositoryBase
{
    public PhotosRepository(IConfiguration configuration) : base(configuration)
    {
    }

    public async Task<bool> UploadRecPhotoAsync(UploadRecordingPhotoRequest request) => await ExecuteSafelyAsync(
        async () =>
        {
            int id = await InsertRecPhotoAsync(request);

            string path = await SaveRecordingPhotoAsync(request.RecordingId, id, request.PhotosBase64, request.Format);

            return await UpdateRecPhotoFilePathAsync(id, path);
        });

    public async Task<bool> UploadUserPhotoAsync(int userId, UserProfilePhotoModel req)
        => await ExecuteSafelyAsync(async () =>
        {
            int id = await InsertUserPhotoAsync(userId, req);
            string path = await SaveUserPhotoAsync(userId, req.PhotoBase64, req.Format);
            return await UpdateUserPhotoFilePathAsync(userId, path);
        });

    private async Task<bool> UpdateUserPhotoFilePathAsync(int userId, string path)
        => await ExecuteSafelyAsync(async () =>
        {
            const string sql = "UPDATE photos SET file_path=@FilePath WHERE user_id = @UserId";
            return await Connection.ExecuteAsync(sql, new { UserId = userId, FilePath = path }) != 0;
        });

    private async Task<bool> UpdateRecPhotoFilePathAsync(int id, string path) => 
        await ExecuteSafelyAsync(async () =>
        {
            const string sql = "UPDATE photos SET file_path=@FilePath WHERE id=@Id";

            return await Connection.ExecuteAsync(sql, new { Id = id, FilePath = path }) != 0;
        });

    private async Task<int> InsertRecPhotoAsync(UploadRecordingPhotoRequest request) => await ExecuteSafelyAsync(async () =>
    {
        const string sql = "INSERT INTO photos(recording_id) VALUES (@RecordingId) RETURNING id;";

        return await Connection.ExecuteScalarAsync<int>(sql, new
        {
            request.RecordingId
        });
    });

    private async Task<int> InsertUserPhotoAsync(int userId, UserProfilePhotoModel req)
    {
        const string sql = "INSERT INTO photos(user_id, format) VALUES (@UserId, @Format) RETURNING id;";
        
        return await Connection.ExecuteScalarAsync<int>(sql, new { UserId = userId, req.Format });
    }
    
    private async Task<string> SaveRecordingPhotoAsync(int recordingId, int photoId, string base64, string format)
    {
        return await FileSystemHelper.SaveRecordingPhotoFileAsync(recordingId, photoId, base64, format);
    }

    private async Task<string> SaveUserPhotoAsync(int userId, string base64, string format)
    {
        return await FileSystemHelper.SaveUserPhotoFileAsync(userId, base64, format);
    }

    public async Task<UserProfilePhotoModel?> GetUserPhotoAsync(int userId) =>
        await ExecuteSafelyAsync(async () =>
        {   
            const string sql = "SELECT * FROM photos WHERE user_id=@UserId";
            Photo? photo = await Connection.QueryFirstOrDefaultAsync<Photo>(sql, new { UserId = userId });
            if (photo is null)
                return null;

            byte[]? data = await ReadPhotoFileAsync(userId, photo.Format);
            if (data is null)
                return null;

            return new UserProfilePhotoModel
            {
                Format = photo.Format,
                PhotoBase64 = Convert.ToBase64String(data),
            };
        });

    private async Task<byte[]?> ReadPhotoFileAsync(int userId, string format)
    {
        return await FileSystemHelper.ReadUserPhotoFileAsync(userId, format);
    }
}