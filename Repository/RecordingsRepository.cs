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
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared.Logging;
using Shared.Models.Database.Dialects;
using Shared.Models.Database.Recordings;
using Shared.Models.Requests.Ai;
using Shared.Models.Requests.Recordings;
using Shared.Tools;
using LogLevel = Shared.Logging.LogLevel;

namespace Repository;

public class RecordingsRepository : RepositoryBase
{
    private readonly TimeSpan _timeMatchTolerance = TimeSpan.FromSeconds(1);
    private readonly ILogger<RecordingsRepository> _logger;
    
    public RecordingsRepository(ILogger<RecordingsRepository> logger, IConfiguration configuration) : base(configuration)
    {
        _logger = logger;
    }

    public async Task<Recording[]?> GetAsync(int? userId, bool parts, bool sound)
    {
        Recording[]? recordings = (userId is not null
            ? await GetByUserIdAsync(userId.Value)
            : await GetAllAsync())?.ToArray();

        if (recordings is null)
            return null;

        if (parts)
            foreach (var recording in recordings)
                recording.Parts = await GetPartsAsync(recording.Id, sound);

        return recordings;
    }

    private async Task<IEnumerable<Recording>?> GetByUserIdAsync(int userId) =>
        await ExecuteSafelyAsync<IEnumerable<Recording>?>(async () =>
        {
            const string sql = """
                               SELECT 
                                   r.*,
                                   COALESCE(SUM(EXTRACT(EPOCH FROM (rp.end_date - rp.start_date))), 0)::DOUBLE PRECISION AS "TotalSeconds"
                               FROM recordings r
                               LEFT JOIN recording_parts rp ON rp.recording_id = r.id
                               WHERE r.user_id = @UserId AND (r.deleted IS FALSE OR r.deleted IS NULL)
                               GROUP BY r.id
                               HAVING r.expected_parts_count = COUNT(rp.id);
                               """;
            return await Connection.QueryAsync<Recording>(sql, new { UserId = userId });
        });

    private async Task<IEnumerable<Recording>?> GetAllAsync() =>
        await ExecuteSafelyAsync<IEnumerable<Recording>?>(async () =>
            await Connection.QueryAsync<Recording>(
                """
                SELECT 
                    r.*,
                    COALESCE(SUM(EXTRACT(EPOCH FROM (rp.end_date - rp.start_date))), 0)::DOUBLE PRECISION AS total_seconds
                FROM recordings r
                LEFT JOIN recording_parts rp ON rp.recording_id = r.id
                WHERE r.deleted IS FALSE OR r.deleted IS NULL
                GROUP BY r.id
                HAVING r.expected_parts_count = COUNT(rp.id);
                """));

    public async Task<IEnumerable<Recording>?> GetDeletedAsync() =>
        await ExecuteSafelyAsync(
            Connection.QueryAsync<Recording>(
                """
                SELECT 
                    r.*,
                    COALESCE(SUM(EXTRACT(EPOCH FROM (rp.end_date - rp.start_date))), 0)::DOUBLE PRECISION AS total_seconds
                FROM recordings r
                LEFT JOIN recording_parts rp ON rp.recording_id = r.id
                WHERE r.deleted = TRUE
                GROUP BY r.id
                HAVING r.expected_parts_count = COUNT(rp.id);
                """));
    

    public async Task<Recording?> GetByIdAsync(int id, bool parts, bool sound)
    {
        var recording = await GetAsync(id);

        if (recording is null || !parts)
            return recording;

        recording.Parts = await GetPartsAsync(id, sound);

        return recording;
    }

    private async Task<Recording?> GetAsync(int id) =>
        await ExecuteSafelyAsync(async () =>
        {
            var r = await Connection.QueryFirstOrDefaultAsync<Recording>(
                """
                    SELECT 
                        r.*,
                        COALESCE(SUM(EXTRACT(EPOCH FROM (rp.end_date - rp.start_date))), 0)::DOUBLE PRECISION AS "TotalSeconds"
                    FROM recordings r
                    LEFT JOIN recording_parts rp ON rp.recording_id = r.id
                    WHERE r.id = @Id
                    GROUP BY r.id
                    """,
                new
                {
                    Id = id
                });
            return r;
        });

    public async Task<IEnumerable<RecordingPart>?> GetPartsAsync(int recordingId, bool sound) =>
        await ExecuteSafelyAsync<IEnumerable<RecordingPart>?>(async () =>
        {
            var parts = await GetPartsAsync(recordingId);

            if (parts is null)
                return null;

            if (!sound)
                return parts;

            var partsArray = parts as RecordingPart[] ?? parts!.ToArray();

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

    private async Task<IEnumerable<RecordingPart>?> GetPartsAsync(int recordingId) =>
        await ExecuteSafelyAsync<IEnumerable<RecordingPart>?>(async () =>
        {
            const string sql = """
                               SELECT *
                               FROM recording_parts 
                               WHERE recording_id = @RecordingId
                               """;
            return await Connection.QueryAsync<RecordingPart>(sql, new { RecordingId = recordingId });
        });

    public async Task<int?> UploadAsync(int userId, RecordingUploadRequest request) =>
        await ExecuteSafelyAsync(async () =>
        {
            const string sql = """
                               INSERT INTO recordings(user_id, created_at, estimated_birds_count, device, by_app, note, name, expected_parts_count)
                               VALUES (@UserId, @CreatedAt, @EstimatedBirdsCount, @Device, @ByApp, @Note, @Name, @ExpectedPartsCount) 
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
                request.Name,
                request.ExpectedPartsCount
            });
        });
    
    public async Task<int?> UploadPartAsync(RecordingPartUploadRequest request)
    {
        int? partId = await UploadPartModelToDbAsync(request);

        if (partId is null)
            return null;

        byte[] content = Convert.FromBase64String(request.DataBase64);
        await SaveSoundFileAsync(request.RecordingId, partId.Value, content);

        return partId;
    }

    public async Task<int?> UploadPartAsync(RecordingPartUploadRequest request, IFormFile file)
    {
        int? partId = await UploadPartModelToDbAsync(request);

        if (partId is null)
            return null;

        var content = new MemoryStream();
        await file.CopyToAsync(content);
        await SaveSoundFileAsync(request.RecordingId, partId.Value, content.ToArray());

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

    private async Task SaveSoundFileAsync(int recordingId, int recordingPartId, byte[] content)
    {
        await FileSystemHelper.SaveOriginalRecordingFileAsync(recordingId, recordingPartId, content);
        string normalizedPath = FileSystemHelper.GetNormalizedRecordingFilePath(recordingId, recordingPartId);
        await FFmpegService.NormalizeAudioAsync(content, outputPath: normalizedPath);

        await UpdateFilePathAsync(recordingPartId, normalizedPath);
    }

    private async Task UpdateFilePathAsync(int recordingId,
        string filePath) =>
        await ExecuteSafelyAsync(
            Connection.ExecuteAsync(
                "UPDATE recording_parts SET file_path = @FilePath WHERE id = @Id",
                new
                {
                    FilePath = filePath,
                    Id = recordingId
                }
            )
        );

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
                dialect.PredictedDialect = dialects.FirstOrDefault(d => d.Id == dialect.PredictedDialectId)?.DialectCode;
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
                        {(verified ? $"{(recordingId is not null ? "AND" : "")} state IN (2, 3, 5, 6, 7)" : "")}
                ", new { RecordingId = recordingId }));

    private async Task<Dialect[]?> GetDialects() =>
        await ExecuteSafelyAsync(async () =>
            (await Connection.QueryAsync<Dialect>("SELECT * FROM dialects")).ToArray());

    private async Task<DetectedDialect[]?> GetDetectedDialects(int filteredPartId) =>
        await ExecuteSafelyAsync(async () =>
            (await Connection.QueryAsync<DetectedDialect>(
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
        await GetByIdAsync(id, false, false) is not null;
    
    public async Task<bool> IsOwnerAsync(int id, int userId)
    {
        if (!await ExistsAsync(id))
            return false;

        return (await GetByIdAsync(id, false, false))!.UserId == userId;
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

    public async Task<RecordingPart?> GetPartAsync(int partId) =>
        await ExecuteSafelyAsync(Connection.QueryFirstOrDefaultAsync<RecordingPart>(
            "SELECT * FROM recording_parts WHERE id = @Id",
            new { Id = partId }));
    
    public async Task<byte[]?> GetPartSoundAsync(int partId)
    {
        var part = await GetPartAsync(partId);
        if (part?.FilePath is null)
            return null;
        
        return await FileSystemHelper.ReadRecordingFileAsync(part.FilePath);
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

    public async Task<bool> UpsertDetectedDialectAsync(
        int filteredPartId,
        string? userGuessDialectCode = null,
        string? confirmedDialectCode = null,
        string? predictedDialectCode = null)
    {
        if (userGuessDialectCode is null && confirmedDialectCode is null && predictedDialectCode is null)
        {
            Logger.Log("RecordingsRepository::UpsertDetectedDialectAsync: All dialect codes are null.", LogLevel.Warning);
            return false;
        }

        var dialectIds = new Dictionary<string, int?>
        {
            ["userGuess"] = userGuessDialectCode is not null ? await GetDialectCodeIdAsync(userGuessDialectCode) : null,
            ["confirmed"] = confirmedDialectCode is not null ? await GetDialectCodeIdAsync(confirmedDialectCode) : null,
            ["predicted"] = predictedDialectCode is not null ? await GetDialectCodeIdAsync(predictedDialectCode) : null
        };

        var setClauses = new List<string>();
        var parameters = new DynamicParameters();

        parameters.Add("FilteredPartId", filteredPartId);
        parameters.Add("UserGuessDialectId", dialectIds["userGuess"]);
        parameters.Add("ConfirmedDialectId", dialectIds["confirmed"]);
        parameters.Add("PredictedDialectId", dialectIds["predicted"]);

        if (dialectIds["userGuess"] is not null)
            setClauses.Add("user_guess_dialect_id = EXCLUDED.user_guess_dialect_id");
        if (dialectIds["confirmed"] is not null)
            setClauses.Add("confirmed_dialect_id = EXCLUDED.confirmed_dialect_id");
        if (dialectIds["predicted"] is not null)
            setClauses.Add("predicted_dialect_id = EXCLUDED.predicted_dialect_id");

        if (setClauses.Count == 0)
            return false;

        string sql = $"""
                      INSERT INTO detected_dialects (
                          filtered_recording_part_id, 
                          user_guess_dialect_id, 
                          confirmed_dialect_id, 
                          predicted_dialect_id
                      )
                      VALUES (
                          @FilteredPartId,
                          @UserGuessDialectId,
                          @ConfirmedDialectId,
                          @PredictedDialectId
                      )
                      ON CONFLICT (filtered_recording_part_id)
                      DO UPDATE SET {string.Join(", ", setClauses)};
                      """;

        return await ExecuteSafelyAsync(Connection.ExecuteAsync(sql, parameters)) != 0;    
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
            Connection.QueryAsync<RecordingPart>(
                "SELECT * FROM recording_parts WHERE start_date = end_date"
            ));

        if (parts is null)
            return;

        foreach (var part in parts)
        {
            Console.WriteLine();
            Logger.Log("Start analyzing sound file for part " + part.Id);
            string format = FFmpegService.DetectFileFormat(part.FilePath);
            Logger.Log("File format: " + format);
            string duration = FFmpegService.GetFileDuration(part.FilePath);
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
        }
    }

    public async Task<Dialect[]> GetDialectsAsync()
    {
        return (await Connection.QueryAsync<Dialect>("SELECT * FROM dialects")).ToArray();
    }

    public async Task NormalizeAudiosAsync()
    {
        var parts = await ExecuteSafelyAsync(
            Connection.QueryAsync<RecordingPart>(
                "SELECT * FROM recording_parts WHERE file_path IS NOT NULL"
            ));

        if (parts is null)
            return;

        foreach (var part in parts)
        {
            Logger.Log("Normalizing audio for part " + part.Id);
            
            byte[] normalizedBadly = await File.ReadAllBytesAsync(part.FilePath);
            await FFmpegService.NormalizeAudioAsync(normalizedBadly, outputPath: "temp");
            
            byte[] normalizedContent = await File.ReadAllBytesAsync("temp");
            await File.WriteAllBytesAsync(part.FilePath, normalizedContent);
            
            File.Delete("temp");
            Logger.Log("Normalized audio saved for part " + part.Id);
        }
    }

    public async Task AnalyzePartsAsync()
    {
        var parts = await ExecuteSafelyAsync(
            Connection.QueryAsync<RecordingPart>(
                "SELECT * FROM recording_parts WHERE file_path IS NOT NULL"
            ));

        if (parts is null)
            return;

        foreach (var part in parts)
        {
            Console.WriteLine();
            Logger.Log("Start analyzing sound file for part " + part.Id);
            string format = FFmpegService.DetectFileFormat(part.FilePath);
            Logger.Log("File format: " + format);
            string duration = FFmpegService.GetFileDuration(part.FilePath);
            Logger.Log("File duration: " + duration + " seconds");
        }
    }

    public async Task<int[]?> GetIncompleteRecordingsAsync(int userId)
    {
        var incomplete = await ExecuteSafelyAsync(Connection.QueryAsync<Recording>(
            """
            SELECT * 
            FROM recordings r
            WHERE r.user_id = @UserId
            AND r.expected_parts_count != (
                SELECT COUNT(*)
                FROM recording_parts rp
                WHERE rp.recording_id = r.id
            )
            """, new { UserId = userId }));
        
        return incomplete?.Select(r => r.Id).ToArray();
    }

    public async Task ProcessPredictionAsync(int recordingPartId, PredicationResult result)
    {
        var part = await GetPartAsync(recordingPartId);
        if (part is null) return;

        int recordingId = part.RecordingId;
        int representantSegmentId = result.RepresentantId;
        for (var i = 0; i < result.Segments.Length; i++)
        {
            var segment = result.Segments[i];
            try
            {
                var startDate = part.StartDate + TimeSpan.FromSeconds(segment.Interval[0]);
                var endDate = part.EndDate + TimeSpan.FromSeconds(segment.Interval[1]);

                var filteredPart = await CreateFilteredPartAsync(recordingId,
                    startDate,
                    endDate,
                    FilteredRecordingPartState.DetectedByAi,
                    representant: representantSegmentId == i);

                if (filteredPart is null) continue;

                await UpsertDetectedDialectAsync(
                    filteredPart.Id,
                    predictedDialectCode: segment.Label
                );

                _logger.LogInformation(
                    "New filtered parts and detected dialects from predication result have been successfully created");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to automatically classify the dialect");
            }
        }
    }
}