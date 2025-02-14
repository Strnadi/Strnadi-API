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
using Microsoft.AspNetCore.Mvc;
using Models.Requests;
using Npgsql;
using Shared.Logging;

namespace DataAccessGate.Sql;

internal class UsersRepository : RepositoryBase
{
    public UsersRepository(string connectionString) : base(connectionString)
    {
    }

    public bool AuthorizeUser(string email, string password)
    {
        using var command = (NpgsqlCommand)_connection.CreateCommand();

        command.CommandText =
            $"SELECT * FROM \"Users\" WHERE \"Email\" = @Email AND \"Password\" = @Password";
        
        command.Parameters.AddWithValue("@Email", email);
        command.Parameters.AddWithValue("@Password", password);

        using var reader = command.ExecuteReader();
        return reader.HasRows;
    }

    public bool ExistsUser(string email)
    {
        using var command = (NpgsqlCommand)_connection.CreateCommand();
        
        command.CommandText =
            $"SELECT * FROM \"Users\" WHERE \"Email\" = @Email";
        
        command.Parameters.AddWithValue("@Email", email);

        return command.ExecuteReader().HasRows;
    }
    
    /// <returns>An id of user with provided email if exists, otherwise -1</returns>
    public int GetUserId(string email)
    {
        if (!ExistsUser(email))
            return -1;
        
        using var command = (NpgsqlCommand)_connection.CreateCommand();
        
        command.CommandText =
            "SELECT \"Id\" FROM \"Users\" WHERE \"Email\" = @Email";
        
        command.Parameters.AddWithValue("@Email", email);

        return (int)command.ExecuteScalar()!;
    }
    
    public bool AddUser(SignUpRequest request)
    {
        using var command = (NpgsqlCommand)_connection.CreateCommand();
        
        command.CommandText = $"INSERT INTO \"Users\" (\"Nickname\", " +
                              $"\"Email\", \"Password\", \"FirstName\", \"LastName\") " +
                              $"VALUES (@Nickname, @Email, @Password, @FirstName, @LastName)";
        
        command.Parameters.AddWithValue("@Nickname", request.Nickname ?? (object) DBNull.Value);
        command.Parameters.AddWithValue("@Email", request.Email);
        command.Parameters.AddWithValue("@Password", request.Password);
        command.Parameters.AddWithValue("@FirstName", request.FirstName);
        command.Parameters.AddWithValue("@LastName", request.LastName);

        try
        {
            command.ExecuteNonQuery();
            return true;
        }
        catch (NpgsqlException ex) when (ex.SqlState == "23505")
        {
            Logger.Log($"Tried to register user with existing email '{request.Email}'");
            return false;
        }
        catch (Exception ex)
        {
            Logger.Log($"Exception thrown while adding new user to a database: {ex.Message}");
            return false;
        }
    }
}