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
        const string sql = "SELECT 1 FROM \"Users\" WHERE \"Email\" = @Email AND \"Password\" = @Password";

        int rowsCount = _connection.QueryFirstOrDefault<int>(sql, new { Email = email, Password = password });

        return rowsCount != 0;
    }

    public bool ExistsUser(string email)
    {
        const string sql = "SELECT 1 FROM \"Users\" WHERE \"Email\" = @Email";

        return _connection.ExecuteScalar<int?>(sql, new { Email = email }) != null;
    }

    /// <returns>An id of user with provided email if exists, otherwise -1</returns>
    public int GetUserId(string email)
    {
        if (!ExistsUser(email))
            return -1;

        const string sql = "SELECT \"Id\" FROM \"Users\" WHERE \"Email\" = @Email";
        
        return _connection.ExecuteScalar<int>(sql, new { Email = email });
    }

    public UserModel? GetUser(string email)
    {
        const string sql = "SELECT * FROM \"Users\" WHERE \"Email\" = @Email";
        
        var user = _connection.QueryFirstOrDefault<UserModel>(sql, new { Email = email });
        
        return user;
    }

    public bool AddUser(SignUpRequest request)
    {
        const string sql = """
                           INSERT INTO "Users" ("Nickname", "Email", "Password", "FirstName", "LastName") 
                                               VALUES (@Nickname, @Email, @Password, @FirstName, @LastName)
                           """;

        try
        {
            var result = _connection.Execute(sql, new
            {
                request.Nickname,
                request.Email,
                request.Password,
                request.FirstName,
                request.LastName
            });

            return result > 0;
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
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

    public bool Verify(string email)
    {
        const string sql = "UPDATE \"Users\" SET \"IsEmailVerified\" = @IsEmailVerified WHERE \"Email\" = @Email";

        try
        {
            var result = _connection.Execute(sql, new { IsEmailVerified = true, Email = email });
            return result > 0;
        }
        catch (Exception ex)
        {
            Logger.Log($"Exception thrown while verifying user with email '{email}': {ex.Message}");
            return false;
        }
    }
}