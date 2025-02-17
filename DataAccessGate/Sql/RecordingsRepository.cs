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
using Models.Database;
using Models.Requests;
using Shared.Logging;
using Shared.Services;
using LogLevel = Shared.Logging.LogLevel;

namespace DataAccessGate.Sql;

public class RecordingsRepository : RepositoryBase
{
    public RecordingsRepository(IConfiguration configuration) : base(configuration)
    {
    }
    
    public int AddRecording(int userId, RecordingUploadReqInternal request)
    {
        const string sql = """
                           INSERT INTO "Recordings"("UserId", "CreatedAt", "EstimatedBirdsCount", "Device", "ByApp", "Note") 
                           VALUES (@UserId, @CreatedAt, @EstimatedBirdsCount, @Device, @ByApp, @Note) 
                           RETURNING "Id"
                           """;
        try
        {
            var parameters = new
            {
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                EstimatedBirdsCount = request.EstimatedBirdsCount,
                Device = request.Device,
                ByApp = request.ByApp,
                Note = request.Note ?? (object)DBNull.Value
            };

            return Connection.ExecuteScalar<int>(sql, parameters);
        }
        catch (Exception ex)
        {
            Logger.Log($"Exception caught while adding new recording: {ex.Message}", LogLevel.Error);
            return -1;
        }
    }

    public int AddRecordingPart(RecordingPartUploadReq request)
    {
        var fs = new FileSystemHelper();

        string insertSql = """
                           INSERT INTO "RecordingParts"(
                                "RecordingId", "Start", "End", "GpsLatitudeStart", "GpsLongitudeStart", "GpsLatitudeEnd", "GpsLongitudeEnd") 
                           VALUES (@RecordingId, @Start, @End, @GpsLatitudeStart, @GpsLongitudeStart, @GpsLatitudeEnd, @GpsLongitudeEnd) 
                           RETURNING "Id"
                           """;

        var insertParameters = new
        {
            RecordingId = request.RecordingId,
            Start = request.Start,
            End = request.End,
            GpsLatitudeStart = request.LatitudeStart,
            GpsLongitudeStart = request.LongitudeStart,
            GpsLatitudeEnd = request.LatitudeEnd,
            GpsLongitudeEnd = request.LongitudeEnd
        };

        try
        {
            int recPartId = Connection.ExecuteScalar<int>(insertSql, insertParameters);

            byte[] binary = EncodingHelper.DecodeFromBase64(request.Data);
            string filePath = fs.SaveRecordingFile(request.RecordingId, recPartId, binary);

            string updatePathSql = "UPDATE \"RecordingParts\" SET \"FilePath\" = @FilePath WHERE \"Id\" = @Id";
            var updatePathParameters = new { FilePath = filePath, Id = recPartId };

            Connection.Execute(updatePathSql, updatePathParameters);
            return recPartId;
        }
        catch (Exception ex)
        {
            Logger.Log($"Exception caught while adding recording part: {ex.Message}", LogLevel.Error);
            return -1;
        }
    }

    public IEnumerable<RecordingModel>? GetUsersRecordings(int userId)
    {
        try
        {
            string sql = "SELECT * FROM \"Recordings\" WHERE \"UserId\" = @UserId";
            return Connection.Query<RecordingModel>(sql, new { UserId = userId });
        }
        catch (Exception ex)
        {
            Logger.Log($"Exception caught while getting recordings of user '{userId}': {ex.Message}");
            return null;
        }
    }
    
    public RecordingModel? GetRecording(int id)
    {
        try
        {
            string sql = "SELECT * FROM \"Recordings\" WHERE \"Id\" = @Id";
            return Connection.QueryFirstOrDefault<RecordingModel>(sql, new { Id = id });
        }
        catch (Exception ex)
        {
            Logger.Log($"Exception caught while getting recording '{id}': {ex.Message}");
            return null;
        }
    }

    public IEnumerable<RecordingPartModel> GetRecordingParts(int recordingId, bool withSound)
    {
        string sql = "SELECT * FROM \"RecordingParts\" WHERE \"RecordingId\" = @RecordingId";

        RecordingPartModel[] models =
            Connection.Query<RecordingPartModel>(sql, new { RecordingId = recordingId }).ToArray();

        if (!withSound)
            return models;

        foreach (var model in models)
        {
            model.DataBase64 = GetRecordingPartData(recordingId, model.Id);
        }

        return models;
    }

    public string GetRecordingPartData(int recordingId, int recordingPartId)
    {
        var fsHelper = new FileSystemHelper();
        
        byte[] binary = fsHelper.ReadRecordingFile(recordingId, recordingPartId);
        return EncodingHelper.EncodeToBase64(binary);
    }
}