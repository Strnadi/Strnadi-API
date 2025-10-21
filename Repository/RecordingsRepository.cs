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
using Shared.Logging;
using Shared.Models.Database.Dialects;
using Shared.Models.Database.Recordings;
using Shared.Models.Requests.Recordings;
using Shared.Tools;
using LogLevel = Shared.Logging.LogLevel;

namespace Repository;

public class RecordingsRepository : RepositoryBase
{
    private readonly TimeSpan _timeMatchTolerance = TimeSpan.FromSeconds(1);
    
    public RecordingsRepository(IConfiguration configuration) : base(configuration)
    {
    }

    public async Task<RecordingModel[]?> GetAsync(int? userId, bool parts, bool sound)
    {
        RecordingModel[]? recordings = (userId is not null
            ? await GetByUserIdAsync(userId.Value)
            : await GetAllAsync())?.ToArray();

        if (recordings is null)
            return null;

        if (parts)
            foreach (var recording in recordings)
                recording.Parts = await GetPartsAsync(recording.Id, sound);

        return recordings;
    }

    private async Task<IEnumerable<RecordingModel>?> GetByUserIdAsync(int userId) =>
        await ExecuteSafelyAsync<IEnumerable<RecordingModel>?>(async () =>
        {
            const string sql = """
                               SELECT r.*,
                                      COALESCE((
                                          SELECT SUM(EXTRACT(EPOCH FROM (rp.end_date - rp.start_date)))
                                          FROM recording_parts rp
                                          WHERE rp.recording_id = r.id
                                      ), 0)::DOUBLE PRECISION AS "TotalSeconds"
                               FROM recordings r
                               WHERE r.user_id = @UserId;
                               """;
            return await Connection.QueryAsync<RecordingModel>(sql, new { UserId = userId });
        });

    private async Task<IEnumerable<RecordingModel>?> GetAllAsync() =>
        await ExecuteSafelyAsync<IEnumerable<RecordingModel>?>(async () =>
            await Connection.QueryAsync<RecordingModel>(
                """
                SELECT r.*,
                       COALESCE((
                           SELECT SUM(EXTRACT(EPOCH FROM (rp.end_date - rp.start_date)))
                           FROM recording_parts rp
                           WHERE rp.recording_id = r.id
                       ), 0)::DOUBLE PRECISION AS total_seconds
                FROM recordings r;
                """));

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
                """
                    SELECT r.*,
                           COALESCE((
                               SELECT SUM(EXTRACT(EPOCH FROM (rp.end_date - rp.start_date)))
                               FROM recording_parts rp
                               WHERE rp.recording_id = r.id
                           ), 0)::DOUBLE PRECISION AS "TotalSeconds"
                    FROM recordings r
                    WHERE r.id = @Id;
                    """,
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
            const string sql = """
                               SELECT *
                               FROM recording_parts 
                               WHERE recording_id = @RecordingId
                               """;
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
        string filePath = await FileSystemHelper.SaveRecordingFileAsync(recordingId, recordingPartId, binary);

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

    public async Task<FilteredRecordingPartModel[]?> GetFilteredPartsAsync(int? recordingId, bool verified)
    {
        var filteredParts = (await GetFilteredPartsRawAsync(recordingId, verified))?.ToArray();

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
            part.DetectedDialects.AddRange(partDialects);
        }
        
        return filteredParts;
    }

    private async Task<IEnumerable<FilteredRecordingPartModel>?> GetFilteredPartsRawAsync(int? recordingId, bool verified) =>
        await ExecuteSafelyAsync(Connection.QueryAsync<FilteredRecordingPartModel>(
                $@"
                    SELECT *
                    FROM filtered_recording_parts
                    {(verified || recordingId is not null ? "WHERE" : "")} 
                        {(recordingId is not null ? "recording_id = @RecordingId" : "")}
                        {(verified ? $"{(recordingId is not null ? "AND" : "")} state IN (2, 3, 5, 7)" : "")}
                ", new { RecordingId = recordingId }));

    private async Task<DialectModel[]?> GetDialects() =>
        await ExecuteSafelyAsync(async () =>
            (await Connection.QueryAsync<DialectModel>("SELECT * FROM dialects")).ToArray());

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
        await ExecuteSafelyAsync(Connection.ExecuteScalarAsync<int>(sql: 
            """
            INSERT INTO filtered_recording_parts(recording_id, start_date, end_date, state)
            VALUES (@RecordingId, @StartDate, @EndDate, @State)
            RETURNING id;
            """, new
            {
                model.RecordingId,
                model.StartDate,
                model.EndDate,
                State = (int)FilteredRecordingPartState.AwaitingProcession
            }));

    public async Task<int?> GetDialectCodeIdAsync(string dialectCode) =>
        await ExecuteSafelyAsync(Connection.ExecuteScalarAsync<int?>(sql:
                "SELECT id FROM dialects WHERE dialect_code = @DialectCode", new
                {
                    DialectCode = dialectCode
                }));

    private async Task<bool> InsertDetectedDialectAsync(int filteredPartId, int userGuessDialectId) =>
        await ExecuteSafelyAsync(Connection.ExecuteAsync(sql:
            """
            INSERT INTO detected_dialects(filtered_recording_part_id, user_guess_dialect_id) 
            VALUES (@FilteredPartId, @UserGuessDialectId)
            """, new
            {
                FilteredPartId = filteredPartId,
                UserGuessDialectId = userGuessDialectId
            })) != 0;

    public async Task<bool> InsertDetectedDialectAsync(int filteredPartId, int? userGuessDialectId,
        int? confirmedDialectId)
    {
        if (userGuessDialectId is null && confirmedDialectId is null)
        {
            Logger.Log("RecordingsRepository::InsertDetectedDialectAsync: Both user guess and confirmed dialect IDs are null. Cannot insert detected dialect.", LogLevel.Warning);
            return false;
        }
        
        return await ExecuteSafelyAsync(Connection.ExecuteAsync(sql:
            """
            INSERT INTO detected_dialects(filtered_recording_part_id, user_guess_dialect_id, confirmed_dialect_id) 
            VALUES (@FilteredPartId, @UserGuessDialectId, @ConfirmedDialectId)
            """, new
            {
                FilteredPartId = filteredPartId,
                UserGuessDialectId = userGuessDialectId,
                ConfirmedDialectId = confirmedDialectId
            })) != 0;
    }

    public async Task<bool> ExistsAsync(int id) =>
        await GetAsync(id, false, false) is not null;
    
    public async Task<bool> IsOwnerAsync(int id, int userId)
    {
        if (!await ExistsAsync(id))
            return false;

        return (await GetAsync(id, false, false))!.UserId == userId;
    }

    public async Task<bool> DeleteAsync(int id, bool final) =>
        await ExecuteSafelyAsync(Connection.ExecuteAsync(
            sql: final
                ? "DELETE FROM recordings WHERE id = @Id"
                : "UPDATE recordings SET deleted = true WHERE id = @Id", 
            new { Id = id } ))!= 0;

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

    public async Task<byte[]> GetPartAsync(int recId, int partId)
    {
        byte[] content = await FileSystemHelper.ReadRecordingFileAsync(recId, partId);
        return content;
    }

    public async Task<FilteredRecordingPartModel?> FindFilteredPartByTimeAsync(
        int recordingId, DateTime start, DateTime end) =>
        await ExecuteSafelyAsync(
            Connection.QueryFirstOrDefaultAsync<FilteredRecordingPartModel>(
                @"
                    SELECT *
                    FROM filtered_recording_parts
                    WHERE recording_id = @RecordingId
                        AND ABS(EXTRACT(EPOCH FROM (start - @Start))) < @ToleranceSeconds
                        AND ABS(EXTRACT(EPOCH FROM (end - @End))) < @ToleranceSeconds
                    LIMIT 1
                ",
                new
                {
                    RecordingId = recordingId,
                    Start = start,
                    End = end,
                    ToleranceSeconds = _timeMatchTolerance.Seconds
                }));

    public async Task<FilteredRecordingPartModel?> CreateFilteredPartAsync(
        int recordingId, DateTime startDate, DateTime endDate, FilteredRecordingPartState state, bool representant) =>
        await ExecuteSafelyAsync(
            Connection.QuerySingleAsync<FilteredRecordingPartModel>(
                """
                    INSERT INTO filtered_recording_parts(recording_id, start_date, end_date, state, representant_flag)
                    VALUES (@RecordingId, @StartDate, @EndDate, @State, @Representant)
                    RETURNING *
                """,
                new
                {
                    RecordingId = recordingId,
                    StartDate = startDate,
                    EndDate = endDate,
                    State = (short)state,
                    Representant = representant
                }
            ));

    public async Task<bool> UpdateFilteredPartAsync(int filteredPartId, DateTime? start, DateTime? end,
        bool? representant) =>
        await ExecuteSafelyAsync(async () =>
        {
            var updateFields = new List<string>();
            var parameters = new DynamicParameters();
            parameters.Add("Id", filteredPartId);
            
            if (start != null)
            {
                updateFields.Add("start_date = @Start");
                parameters.Add("@Start", start.Value);
            }

            if (end != null)
            {
                updateFields.Add("end_date = @End");
                parameters.Add("@End", end.Value);
            }

            if (representant != null)
            {
                updateFields.Add("representant_flag = @Representant");
                parameters.Add("@Representant", representant.Value);
            }

            var sql = $"UPDATE filtered_recording_parts SET {string.Join(", ", updateFields)} WHERE id = @Id";
            
            return await Connection.ExecuteAsync(sql, parameters) != 0;
        });

    public async Task<bool> SetConfirmedDialect(int filteredPartId, string confirmedDialectCode)
    {
        int? dialectId = await GetDialectCodeIdAsync(confirmedDialectCode);
        if (dialectId == null)
            return false;

        return await ExecuteSafelyAsync(
            Connection.ExecuteAsync(sql:
                "UPDATE detected_dialects SET confirmed_dialect_id = @DialectId WHERE filtered_recording_part_id = @PartId ",
                new
                {
                    DialectId = dialectId,
                    PartId = filteredPartId
                }
            )) != 0;
    }

    public async Task<bool> DeleteFilteredPartAsync(int filteredPartId) =>
        await ExecuteSafelyAsync(
            Connection.ExecuteAsync(
                "DELETE FROM filtered_recording_parts WHERE id = @FilteredPartId",
                new
                {
                    FilteredPartId = filteredPartId
                })) != 0;

    public async Task<bool> ExistsFilteredPartAsync(int filteredPartId) =>
        await ExecuteSafelyAsync(
            Connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM filtered_recording_parts WHERE id = @Id",
                new
                {
                    Id = filteredPartId
                })
        ) != 0;

    public async Task FixSameDatesInPartsAsync()
    {
        var parts = await ExecuteSafelyAsync(
            Connection.QueryAsync<RecordingPartModel>(
                "SELECT * FROM recording_parts WHERE start_date = end_date"
            ));

        if (parts is null)
            return;

        foreach (var part in parts)
        {
            Console.WriteLine();
            // try
            // {
            //     using var reader = new AudioFileReader(part.FilePath);
            //     var duration = reader.TotalTime;
            //     var newEndDate = part.StartDate.Add(duration);
            //     Logger.Log("Part id: " + part.Id);
            //     Logger.Log("Start date: " + part.StartDate);
            //     Logger.Log("Old end date: " + part.EndDate);
            //     Logger.Log("New end date: " + newEndDate);
            // }
            // catch (Exception ex)
            // {
            //     Logger.Log("Failed to fix part " + part.Id + ": " + ex, LogLevel.Error);
            // }
            //
            // try
            // {
                FFmpegService ffmpeg = new();
                Logger.Log("Start analyzing sound file for part " + part.Id);
                string format = ffmpeg.DetectFileFormat(part.FilePath);
                Logger.Log("File format: " + format);
                string duration = ffmpeg.GetFileDuration(part.FilePath);
                Logger.Log("File duration: " + duration + " seconds");
                if (double.TryParse(duration, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double seconds))
                {
                    TimeSpan ts = TimeSpan.FromSeconds(seconds);
                    var newEndDate = part.StartDate.Add(ts);
                    Logger.Log("Calculated new end date: " + newEndDate);
                    await Connection.ExecuteAsync("UPDATE recording_parts SET end_date = @EndDate WHERE id = @Id", new
                    {
                        EndDate = newEndDate,
                        Id = part.Id
                    });
                    Logger.Log($"Updated part {part.Id} end date to {newEndDate}");
                }                
            // }
            // catch
            // {
            //     Logger.Log("Failed to detect file format " + part.Id, LogLevel.Error);
            // }
        }
    }

    public async Task<DialectModel[]> GetDialectsAsync()
    {
        return (await Connection.QueryAsync<DialectModel>("SELECT * FROM dialects")).ToArray();
    }
}