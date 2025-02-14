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
        using var command = (NpgsqlCommand)_connection.CreateCommand();

        command.CommandText =
            "INSERT INTO \"Recordings\"(\"UserId\", \"CreatedAt\", \"EstimatedBirdsCount\", \"Device\", \"ByApp\", \"Note\")" +
            "VALUES (@UserId, @CreatedAt, @EstimatedBirdsCount, @Device, @ByApp, @Note) RETURNING \"Id\"";

        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);
        command.Parameters.AddWithValue("@EstimatedBirdsCount", request.EstimatedBirdsCount);
        command.Parameters.AddWithValue("@Device", request.Device);
        command.Parameters.AddWithValue("@ByApp", request.ByApp);
        command.Parameters.AddWithValue("@Note", request.Note ?? (object)DBNull.Value);

        try
        {
            return (int)command.ExecuteScalar()!;
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

        using var insertCmd = (NpgsqlCommand)_connection.CreateCommand();

        insertCmd.CommandText =
            $"INSERT INTO \"RecordingParts\"(\"RecordingId\", \"Start\", \"End\", \"GpsLatitudeStart\", \"GpsLongitudeStart\", \"GpsLatitudeEnd\", \"GpsLontitudeEnd\")" +
            $"VALUES (@RecordingId, @Start, @End, @GpsLatitudeStart, @GpsLongitudeStart, @GpsLatitudeEnd, @GpsLongitudeEnd) RETURNING \"Id\"";

        insertCmd.Parameters.AddWithValue("@RecordingId", request.RecordingId);
        insertCmd.Parameters.AddWithValue("@Start", request.Start);
        insertCmd.Parameters.AddWithValue("@End", request.End);
        insertCmd.Parameters.AddWithValue("@GpsLatitudeStart", request.LatitudeStart);
        insertCmd.Parameters.AddWithValue("@GpsLongitudeStart", request.LongitudeStart);
        insertCmd.Parameters.AddWithValue("@GpsLatitudeEnd", request.LatitudeEnd);
        insertCmd.Parameters.AddWithValue("@GpsLongitudeEnd", request.LongitudeEnd);

        int recPartId;
        try
        {
            recPartId = (int)insertCmd.ExecuteScalar()!;
        }
        catch (NpgsqlException ex)
        {
            Logger.Log($"Exception caught while adding recording part: {ex.Message}", LogLevel.Error);
            return -1;
        }

        byte[] binary = EncodingHelper.DecodeFromBase64(request.Data);
        string filePath = fs.SaveRecordingSoundFile(request.RecordingId, recPartId, binary);

        using var updatePathCmd = (NpgsqlCommand)_connection.CreateCommand();

        updatePathCmd.CommandText =
            "UPDATE \"RecordingParts\" SET \"FilePath\" = @FilePath WHERE \"RecordingId\" = @RecordingId";

        updatePathCmd.Parameters.AddWithValue("@FilePath", filePath);
        updatePathCmd.Parameters.AddWithValue("@RecordingId", request.RecordingId);

        try
        {
            updatePathCmd.ExecuteNonQuery();
            return recPartId;
        }
        catch (Exception ex)
        {
            Logger.Log($"Exception caught while adding recording part: {ex.Message}", LogLevel.Error);
            return -1;
        }
    }
}