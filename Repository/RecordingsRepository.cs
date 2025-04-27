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

using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using Dapper;
using Microsoft.Extensions.Configuration;
using Shared.Models.Database.Dialects;
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

    public async Task<RecordingModel[]?> GetAsync(int? userId, bool parts, bool sound)
    {
        RecordingModel[]? recordings = (userId is not null
            ? await GetByEmailAsync(userId.Value)
            : await GetAllAsync())?.ToArray();

        if (recordings is null)
            return null;

        if (parts)
            foreach (var recording in recordings)
                recording.Parts = await GetPartsAsync(recording.Id, sound);

        return recordings;
    }

    private async Task<IEnumerable<RecordingModel>?> GetByEmailAsync(int userId) =>
        await ExecuteSafelyAsync<IEnumerable<RecordingModel>?>(async () =>
        {
            const string sql = """
                               SELECT * 
                               FROM recordings
                               WHERE user_id = @UserId
                               """;
            return await Connection.QueryAsync<RecordingModel>(sql, new { UserId = userId });
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
            var parts = await GetPartsAsync(recordingId);

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

    private async Task<IEnumerable<RecordingPartModel>?> GetPartsAsync(int recordingId) =>
        await ExecuteSafelyAsync<IEnumerable<RecordingPartModel>?>(async () =>
        {
            const string sql = "SELECT * FROM recording_parts WHERE recording_id = @RecordingId";
            return await Connection.QueryAsync<RecordingPartModel>(sql, new { RecordingId = recordingId });
        });

    public async Task<int?> UploadAsync(int userId, RecordingUploadRequest request) =>
        await ExecuteSafelyAsync(async () =>
        {
            const string sql = """
                               INSERT INTO recordings(user_id, created_at, estimated_birds_count, device, by_app, note, name)
                               VALUES (@UserId, @CreatedAt, @EstimatedBirdsCount, @Device, @ByApp, @Note, @Name) 
                               RETURNING id
                               """;
            return await Connection.ExecuteScalarAsync<int?>(sql, new
            {
                UserId = userId,
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

    private async Task UpdateFilePathAsync(int recordingId,
        string filePath) =>
        await ExecuteSafelyAsync(async () => await Connection.ExecuteAsync(
            "UPDATE recording_parts SET file_path = @FilePath WHERE id = @Id",
            new
            {
                FilePath = filePath,
                Id = recordingId
            }));

    public async Task<FilteredRecordingPartModel[]?> GetFilteredPartsAsync(int recordingId, bool verified)
    {
        var filteredParts = (verified
                ? await GetVerifiedFilteredPartsAsync(recordingId)
                : await GetAllFilteredPartsAsync(recordingId)
            )?.ToArray();

        if (filteredParts is null)
            return null;

        var dialects = await GetDialects();
        if (dialects is null)
            return null;

        foreach (var part in filteredParts)
        {
            var partDialects = await GetDetectedDialects(part.Id) ?? [];
            foreach (var dialect in partDialects)
            {
                dialect.UserGuessDialect = dialects.FirstOrDefault(d => d.Id == dialect.UserGuessDialectId)?.DialectCode;
                dialect.ConfirmedDialect = dialects.FirstOrDefault(d => d.Id == dialect.ConfirmedDialectId)?.DialectCode;
            }
        }
        
        return filteredParts;
    }

    private async Task<IEnumerable<FilteredRecordingPartModel>?> GetAllFilteredPartsAsync(int recordingId) =>
        await ExecuteSafelyAsync(async () => await Connection.QueryAsync<FilteredRecordingPartModel>(sql:
            """
            SELECT * 
            FROM filtered_recording_parts 
            WHERE recording_id = @RecordingId
            """, new { RecordingId = recordingId }));

    private async Task<IEnumerable<FilteredRecordingPartModel>?> GetVerifiedFilteredPartsAsync(int recordingId) =>
        await ExecuteSafelyAsync(async () =>
            await Connection.QueryAsync<FilteredRecordingPartModel>(
                """
                    SELECT *
                    FROM filtered_recording_parts
                    WHERE 
                        recording_id = @RecordingId
                        AND state IN (1, 2)
                """, new { RecordingId = recordingId }));

    private async Task<DialectModel[]?> GetDialects() =>
        await ExecuteSafelyAsync(async () =>
            (await Connection.QueryAsync<DialectModel>("SELECT * FROM dialects"))?.ToArray());

    private async Task<DetectedDialectModel[]?> GetDetectedDialects(int filteredPartId) =>
        await ExecuteSafelyAsync(async () =>
            (await Connection.QueryAsync<DetectedDialectModel>(
                "SELECT * FROM detected_dialects WHERE filtered_recording_part_id = @FPartId",
                new
                {
                    FPartId = filteredPartId
                })).ToArray());

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
    
    public async Task<bool> IsOwnerAsync(int id, int userId)
    {
        if (!await ExistsAsync(id))
            return false;

        return (await GetAsync(id, false, false))!.UserId == userId;
    }

    public async Task<bool> DeleteAsync(int id, bool final) =>
        await ExecuteSafelyAsync(async () =>
            await Connection.ExecuteAsync(
                sql: final
                    ? "DELETE FROM recordings WHERE id = @Id"
                    : "UPDATE recordings SET deleted = true WHERE id = @Id", 
                new { Id = id }) != 0);

    public async Task<bool> UpdateAsync(int recordingId, UpdateRecordingRequest request) =>
        await ExecuteSafelyAsync(async () =>
        {
            var updateFields = new List<string>();
            var parameters = new DynamicParameters();
            parameters.Add("Id", recordingId);

            foreach (var prop in request.GetType().GetProperties().Where(p => p.GetCustomAttribute<ColumnAttribute>() is not null))
            {
                string columnName = prop.GetCustomAttribute<ColumnAttribute>()!.Name!;
                updateFields.Add($"{columnName} = @{prop.Name}");
                parameters.Add(prop.Name, prop.GetValue(request));
            }

            var sql = $"UPDATE recordings SET {string.Join(", ", updateFields)} WHERE id = @Id";
            
            return await Connection.ExecuteAsync(sql, parameters) != 0;
        });
}