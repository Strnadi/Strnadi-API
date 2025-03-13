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
using Shared.Models;
using Shared.Models.Requests;
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
                               FROM "Recordings" 
                               WHERE "UserEmail" = @Email
                               """;
            return await Connection.QueryAsync<RecordingModel>(sql, new { Email = email });
        });

    private async Task<IEnumerable<RecordingModel>?> GetAllAsync() =>
        await ExecuteSafelyAsync<IEnumerable<RecordingModel>?>(async () =>
        {
            const string sql = """SELECT * FROM "Recordings" """;
            return await Connection.QueryAsync<RecordingModel>(sql);
        });

    public async Task<RecordingModel?> GetAsync(int id, bool parts, bool sound)
    {
        var recording = await GetAsync(id);
            
        if (recording is null || !parts)
            return recording;
            
        recording.Parts = await GetPartsAsync(id, sound);
            
        return recording;
    }
    
    private async Task<RecordingModel?> GetAsync(int id) =>
        await ExecuteSafelyAsync(async () => await Connection.QueryFirstOrDefaultAsync<RecordingModel>(
            """SELECT * FROM "Recordings" WHERE "Id" = @Id""",
            new
            {
                Id = id
            }));

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
            const string sql = """SELECT * FROM "RecordingParts" WHERE "RecordingId" = @RecordingId""";
            return await Connection.QueryAsync<RecordingPartModel>(sql, new { RecordingId = recordingId });
        });

    public async Task<int?> UploadAsync(string email, RecordingUploadModel model) =>
        await ExecuteSafelyAsync(async () =>
        {
            const string sql = """
                               INSERT INTO "Recordings"("UserEmail", "CreatedAt", "EstimatedBirdsCount", "Device", "ByApp", "Note", "Name")
                               VALUES (@UserEmail, @CreatedAt, @EstimatedBirdsCount, @Device, @ByApp, @Note, @Name) 
                               RETURNING "Id"
                               """;
            return await Connection.ExecuteScalarAsync<int?>(sql, new
            {
                UserEmail = email,
                model.CreatedAt,
                model.EstimatedBirdsCount,
                model.Device,
                model.ByApp,
                model.Note,
                model.Name
            });
        });

    public async Task<int?> UploadPartAsync(RecordingPartUploadModel model)
    {
        int? partId = await UploadPartModelToDbAsync(model);

        if (partId is null)
            return null;
        
        await SaveSoundFileAsync(model.RecordingId, partId.Value, model.DataBase64);
        
        return partId;
    }

    private async Task<int?> UploadPartModelToDbAsync(RecordingPartUploadModel model) =>
        await ExecuteSafelyAsync(async () =>
        {
            const string sql = """
                               INSERT INTO "RecordingParts"("RecordingId", "Start", "End", "GpsLatitudeStart", "GpsLongitudeStart", "GpsLatitudeEnd", "GpsLongitudeEnd")
                               VALUES (@RecordingId, @Start, @End, @GpsLatitudeStart, @GpsLongitudeStart, @GpsLatitudeEnd, @GpsLongitudeEnd)
                               RETURNING "Id"
                               """;

            return await Connection.ExecuteScalarAsync<int?>(sql, new
            {
                model.RecordingId,
                model.Start,
                model.End,
                model.GpsLatitudeStart,
                model.GpsLongitudeStart,
                model.GpsLatitudeEnd,
                model.GpsLongitudeEnd
            });
        });

    private async Task SaveSoundFileAsync(int recordingId, int recordingPartId, string base64)
    {
        byte[] binary = Convert.FromBase64String(base64);
        string filePath = _fileSystemHelper.SaveRecordingFile(recordingId, recordingPartId, binary);

        await UpdateFilePathAsync(recordingPartId, filePath);
    }

    private async Task UpdateFilePathAsync(int recordingPartId,
        string filePath) =>
        await ExecuteSafelyAsync(async () => await Connection.ExecuteAsync(
            """UPDATE "RecordingParts" SET "FilePath" = @FilePath WHERE "Id" = @Id""",
            new
            {
                FilePath = filePath,
                Id = recordingPartId
            }));

    public async Task<FilteredRecordingPartModel[]?> GetFilteredParts(int recordingPartId, bool verified) =>
        (verified
            ? await GetVerifiedFilteredPartsAsync(recordingPartId)
            : await GetAllFilteredPartsAsync(recordingPartId))?.ToArray();

    private async Task<IEnumerable<FilteredRecordingPartModel>?> GetAllFilteredPartsAsync(int recordingId) =>
        await ExecuteSafelyAsync(async () =>
        {
            const string sql = $"""
                                   SELECT * 
                                   FROM "FilteredRecordingParts" 
                                   WHERE "RecordingPartId" = @RecordingPartId
                                """;

            return await Connection.QueryAsync<FilteredRecordingPartModel>(sql, new { RecordingPartId = recordingId });
        });
    
    private async Task<IEnumerable<FilteredRecordingPartModel>?> GetVerifiedFilteredPartsAsync(int recordingId) =>
        await ExecuteSafelyAsync(async () =>
        {
            const string sql = $"""
                                   SELECT *
                                   FROM "FilteredRecordingParts"
                                   WHERE 
                                       "RecordingPartId" = @RecordingPartId
                                       AND "State" IN (1, 2)
                                """;
            
            return await Connection.QueryAsync<FilteredRecordingPartModel>(sql, new { RecordingPartId = recordingId });
        });
}