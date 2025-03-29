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

    public async Task<bool> UploadUserPhotoAsync(string email, UserProfilePhotoModel req)
        => await ExecuteSafelyAsync(async () =>
        {
            int id = await InsertUserPhotoAsync(email, req);
            string path = await SaveUserPhotoAsync(email, req.PhotoBase64, req.Format);
            return await UpdateUserPhotoFilePathAsync(email, path);
        });

    private async Task<bool> UpdateUserPhotoFilePathAsync(string email, string path)
        => await ExecuteSafelyAsync(async () =>
        {
            const string sql = "UPDATE photos SET file_path=@FilePath WHERE user_email=@UserEmail";
            return await Connection.ExecuteAsync(sql, new { UserEmail = email, FilePath = path }) != 0;
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

    private async Task<int> InsertUserPhotoAsync(string email, UserProfilePhotoModel req)
    {
        const string sql = "INSERT INTO photos(user_email, format) VALUES (@UserEmail, @Format) RETURNING id;";
        
        return await Connection.ExecuteScalarAsync<int>(sql, new { UserEmail = email, req.Format });
    }
    
    private async Task<string> SaveRecordingPhotoAsync(int recordingId, int photoId, string base64, string format)
    {
        var fs = new FileSystemHelper();
        return await fs.SaveRecordingPhotoFileAsync(recordingId, photoId, base64, format);
    }

    private async Task<string> SaveUserPhotoAsync(string email, string base64, string format)
    {
        var fs = new FileSystemHelper();
        return await fs.SaveUserPhotoFileAsync(email, base64, format);
    }

    public async Task<UserProfilePhotoModel?> GetUserPhotoAsync(string email) =>
        await ExecuteSafelyAsync(async () =>
        {   
            const string sql = "SELECT * FROM photos WHERE user_email=@Email";
            PhotoModel? photo = await Connection.QuerySingleOrDefaultAsync<PhotoModel>(sql, new { Email = email });
            if (photo is null)
                return null;

            string? data = await ReadPhotoFileAsync(email, photo.Format);
            if (data is null)
                return null;

            return new UserProfilePhotoModel()
            {
                Format = photo.Format,
                PhotoBase64 = data,
            };
        });

    private async Task<string?> ReadPhotoFileAsync(string email, string format)
    {
        var fs = new FileSystemHelper();
        return await fs.ReadUserPhotoFileAsync(email, format);
    }
}