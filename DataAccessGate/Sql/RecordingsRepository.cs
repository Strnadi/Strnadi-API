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
using Models.Requests;
using Npgsql;
using Shared.Logging;
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
            "INSERT INTO \"Recordings\"(\"UserId\", \"CreatedAt\", \"EstimatedBirdsCount\", \"State\", \"Device\", \"ByApp\", \"Note\")" +
            "VALUES (@UserId, @CreatedAt, @EstimatedBirdsCount, @State, @Device, @ByApp, @Note) RETURNING \"Id\"";

        command.Parameters.AddWithValue("@UserId", userId.ToString());

        try
        {
            return (int)command.ExecuteScalar();
        }
        catch (Exception ex)
        {
            Logger.Log($"Exception caught while adding new recording: {ex.Message}", LogLevel.Error);
            return -1;
        }
    }
}