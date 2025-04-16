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
using Shared.Models.Database.Recordings;
using Shared.Models.Requests.Recordings;
using Shared.Tools;

namespace Repository;

public class RecordingsRepository : RepositoryBase
{
    private readonly FileSystemHelper _fileSystemHelper;
    
    public RecordingsRepository(IConfiguration configuration) : base(configuration)
    {
        _fileSystemHelper = new FileSystemHelper();
    }

    public async Task<RecordingModel[]?> GetAsync(string? email, bool parts, bool sound)
    {
        RecordingModel[]? recordings = (email is not null
            ? await GetByEmailAsync(email)
            : await GetAllAsync())?.ToArray();

        if (recordings is null)
            return null;

        if (parts)
            foreach (var recording in recordings)
                recording.Parts = await GetPartsAsync(recording.Id, sound);

        return recordings;
    }

    private async Task<IEnumerable<RecordingModel>?> GetByEmailAsync(string email) =>
        await ExecuteSafelyAsync<IEnumerable<RecordingModel>?>(async () =>
        {
            const string sql = """
                               SELECT * 
                               FROM recordings
                               WHERE user_email = @Email
                               """;
            return await Connection.QueryAsync<RecordingModel>(sql, new { Email = email });
        });

    private async Task<IEnumerable<RecordingModel>?> GetAllAsync() =>
        await ExecuteSafelyAsync<IEnumerable<RecordingModel>?>(async () => 
            await Connection.QueryAsync<RecordingModel>("SELECT * FROM recordings"));

    public async Task<RecordingModel?> GetAsync(int id, bool parts, bool sound)
    {
        var recording = await GetAsync(id);
            
        if (recording is null || !parts)
            return recording;
            
        recording.Parts = await GetPartsAsync(id, sound);
            
        return recording;
    }
    
    private async Task<RecordingModel?> GetAsync(int id) =>
        await ExecuteSafelyAsync(async () =>
        {
            var r = await Connection.QueryFirstOrDefaultAsync<RecordingModel>(
                "SELECT * FROM recordings WHERE id = @Id",
                new
                {
                    Id = id
                });
            return r;
        });

    public async Task<IEnumerable<RecordingPartModel>?> GetPartsAsync(int recordingId, bool sound) =>
        await ExecuteSafelyAsync<IEnumerable<RecordingPartModel>?>(async () =>
        {
            var parts = await GetPartsUnsafeAsync(recordingId);

            if (parts is null)
                return null;

            if (!sound)
                return parts;

            var partsArray = parts as RecordingPartModel[] ?? parts!.ToArray();
            
            foreach (var part in partsArray)
            {
                string? path = part.FilePath;

                if (path is null)
                    continue;

                var bytes = await File.ReadAllBytesAsync(path);
                part.DataBase64 = Convert.ToBase64String(bytes);
            }

            return partsArray;
        });

    private async Task<IEnumerable<RecordingPartModel>?> GetPartsUnsafeAsync(int recordingId) =>
        await ExecuteSafelyAsync<IEnumerable<RecordingPartModel>?>(async () =>
        {
            const string sql = "SELECT * FROM recording_parts WHERE recording_id = @RecordingId";
            return await Connection.QueryAsync<RecordingPartModel>(sql, new { RecordingId = recordingId });
        });

    public async Task<int?> UploadAsync(string email, RecordingUploadRequest request) =>
        await ExecuteSafelyAsync(async () =>
        {
            const string sql = """
                               INSERT INTO recordings(user_email, created_at, estimated_birds_count, device, by_app, note, name)
                               VALUES (@UserEmail, @CreatedAt, @EstimatedBirdsCount, @Device, @ByApp, @Note, @Name) 
                               RETURNING id
                               """;
            return await Connection.ExecuteScalarAsync<int?>(sql, new
            {
                UserEmail = email,
                request.CreatedAt,
                request.EstimatedBirdsCount,
                request.Device,
                request.ByApp,
                request.Note,
                request.Name
            });
        });

    public async Task<int?> UploadPartAsync(RecordingPartUploadRequest request)
    {
        int? partId = await UploadPartModelToDbAsync(request);

        if (partId is null)
            return null;
        
        await SaveSoundFileAsync(request.RecordingId, partId.Value, request.DataBase64);
        
        return partId;
    }

    private async Task<int?> UploadPartModelToDbAsync(RecordingPartUploadRequest request) =>
        await ExecuteSafelyAsync(async () =>
        {
            const string sql = """
                               INSERT INTO recording_parts(
                                   recording_id, start_date, end_date, gps_latitude_start, gps_longitude_start, gps_latitude_end, gps_longitude_end
                               )
                               VALUES (@RecordingId, @StartDate, @EndDate, @GpsLatitudeStart, @GpsLongitudeStart, @GpsLatitudeEnd, @GpsLongitudeEnd)
                               RETURNING id
                               """;

            return await Connection.ExecuteScalarAsync<int?>(sql, new
            {
                request.RecordingId,
                request.StartDate,
                request.EndDate,
                request.GpsLatitudeStart,
                request.GpsLongitudeStart,
                request.GpsLatitudeEnd,
                request.GpsLongitudeEnd
            });
        });

    private async Task SaveSoundFileAsync(int recordingId, int recordingPartId, string base64)
    {
        byte[] binary = Convert.FromBase64String(base64);
        string filePath = await _fileSystemHelper.SaveRecordingFileAsync(recordingId, recordingPartId, binary);

        await UpdateFilePathAsync(recordingPartId, filePath);
    }

    private async Task UpdateFilePathAsync(int recordingPartId,
        string filePath) =>
        await ExecuteSafelyAsync(async () => await Connection.ExecuteAsync(
            "UPDATE recording_parts SET file_path = @FilePath WHERE id = @Id",
            new
            {
                FilePath = filePath,
                Id = recordingPartId
            }));

    public async Task<FilteredRecordingPartModel[]?> GetFilteredPartsAsync(int recordingPartId, bool verified) =>
        (verified
            ? await GetVerifiedFilteredPartsAsync(recordingPartId)
            : await GetAllFilteredPartsAsync(recordingPartId))?.ToArray();

    private async Task<IEnumerable<FilteredRecordingPartModel>?> GetAllFilteredPartsAsync(int recordingId) =>
        await ExecuteSafelyAsync(async () => await Connection.QueryAsync<FilteredRecordingPartModel>(sql:
            """
            SELECT * 
            FROM filtered_recording_parts 
            WHERE recording_part_id = @RecordingPartId
            """, new { RecordingPartId = recordingId }));
    
    private async Task<IEnumerable<FilteredRecordingPartModel>?> GetVerifiedFilteredPartsAsync(int recordingId) =>
        await ExecuteSafelyAsync(async () => 
            await Connection.QueryAsync<FilteredRecordingPartModel>(
            """
                SELECT *
                FROM filtered_recording_parts
                WHERE 
                    recording_part_id = @RecordingPartId
                    AND state IN (1, 2)
            """, new { RecordingPartId = recordingId }));

    public async Task<bool> UploadFilteredPartAsync(FilteredRecordingPartUploadRequest model)
    {
        int? dialectId = await GetDialectCodeIdAsync(model.DialectCode);

        if (dialectId is null)
            return false;
        
        int filteredPartId = await InsertFilteredPartAsync(model);

        return await InsertDetectedDialectAsync(filteredPartId, dialectId.Value);
    }

    private async Task<int> InsertFilteredPartAsync(FilteredRecordingPartUploadRequest model) =>
        await ExecuteSafelyAsync(async () => await Connection.ExecuteScalarAsync<int>(sql: 
            """
            INSERT INTO filtered_recording_parts(recording_id, start_date, end_date)
            VALUES (@RecordingId, @StartDate, @EndDate)
            RETURNING id;
            """, new
            {
                model.RecordingId,
                model.StartDate,
                model.EndDate
            }));

    private async Task<int?> GetDialectCodeIdAsync(string dialectCode) =>
        await ExecuteSafelyAsync(async () =>
            await Connection.ExecuteScalarAsync<int?>(sql:
                "SELECT id FROM dialects WHERE dialect_code = @DialectCode", new
                {
                    DialectCode = dialectCode
                }));

    private async Task<bool> InsertDetectedDialectAsync(int filteredPartId, int dialectId) =>
        await ExecuteSafelyAsync(async () =>
            await Connection.ExecuteAsync(sql:
                """
                INSERT INTO detected_dialects(filtered_recording_part_id, user_guess_dialect_id) 
                VALUES (@FilteredPartId, @UserGuessDialectId)
                """, new
                {
                    FilteredPartId = filteredPartId,
                    UserGuessDialectId = dialectId
                }) != 0);

    public async Task<bool> ExistsAsync(int id) =>
        await GetAsync(id, false, false) is not null;
    
    public async Task<bool> IsOwnerAsync(int id, string email)
    {
        if (!await ExistsAsync(id))
            return false;

        return (await GetAsync(id, false, false))!.UserEmail == email;
    }

    public async Task<bool> DeleteAsync(int id) =>
        await ExecuteSafelyAsync(async () =>
            await Connection.ExecuteAsync(sql:
                "DELETE FROM recordings WHERE id = @Id", new { Id = id })) != 0;
}