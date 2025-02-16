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
using DataAccessGate.Services;
using Models.Requests;
using Npgsql;
using Shared.Logging;
using Shared.Services;
using LogLevel = Shared.Logging.LogLevel;

namespace DataAccessGate.Sql;

internal class RecordingsRepository : RepositoryBase
{
    public RecordingsRepository(string connectionString) : base(connectionString)
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

            return _connection.ExecuteScalar<int>(sql, parameters);
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

        const string insertSql = """

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
            int recPartId = _connection.ExecuteScalar<int>(insertSql, insertParameters);

            byte[] binary = EncodingHelper.DecodeFromBase64(request.Data);
            string filePath = fs.SaveRecordingSoundFile(request.RecordingId, recPartId, binary);

            const string updatePathSql = "UPDATE \"RecordingParts\" SET \"FilePath\" = @FilePath WHERE \"Id\" = @Id";
            var updatePathParameters = new { FilePath = filePath, Id = recPartId };

            _connection.Execute(updatePathSql, updatePathParameters);
            return recPartId;
        }
        catch (Exception ex)
        {
            Logger.Log($"Exception caught while adding recording part: {ex.Message}", LogLevel.Error);
            return -1;
        }
    }
}