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
using Microsoft.Extensions.Logging;
using Shared.Logging;
using Shared.Tools;
using Shared.Models.Database;
using Shared.Models.Requests.Auth;
using Shared.Models.Requests.Users;
using LogLevel = Shared.Logging.LogLevel;

namespace Repository;

public class UsersRepository : RepositoryBase
{
    public UsersRepository(IConfiguration configuration) : base(configuration)
    {
    }
    
    public async Task<bool> ExistsAsync(string email) =>
        await ExecuteSafelyAsync(async () => 
            await Connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM users WHERE email = @Email", 
                new { Email = email }
                )) != 0;
    
    public async Task<bool> ExistsAppleAsync(string appleId) =>
        await ExecuteSafelyAsync(async () => 
            await Connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM users WHERE appleid = @AppleId", 
                new { AppleId = appleId }
                )) != 0;

    public async Task<bool> ExistsAsync(int userId) =>
        await ExecuteSafelyAsync(async () =>
            await Connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM users WHERE id = @Id",
                new { Id = userId }
            )) != 0;
    
    public async Task<bool> ExistsGoogleAsync(string googleId) => 
        await ExecuteSafelyAsync(async () => 
            await Connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM users WHERE google_id = @GoogleId",
                new { GoogleId = googleId }
            )) != 0;
    

    public async Task<bool> AuthorizeAsync(string email, string password) =>
        await ExecuteSafelyAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                return false;
            }
            
            const string sql = "SELECT password FROM users WHERE email = @Email";

            if (!await ExistsAsync(email))
                return false;

            string oldHashedPassword = await Connection.ExecuteScalarAsync<string>(sql,
                                           new
                                           {
                                               Email = email
                                           }) ??
                                       throw new Exception("Failed to get password from database");

            var bcrypt = new BCryptService();
            return bcrypt.VerifyPassword(password, oldHashedPassword!);
        });

    public async Task<bool> CreateUserAsync(SignUpRequest request, bool regularRegister) =>
        await ExecuteSafelyAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(request.Email) || (regularRegister && string.IsNullOrWhiteSpace(request.Password)))
            {
                return false;
            }
                
            const string sql =
                """
                INSERT INTO users(nickname, email, password, first_name, last_name, post_code, city, consent, appleid, google_id) 
                VALUES (@Nickname, @Email, @Password, @FirstName, @LastName, @PostCode, @City, @Consent, @AppleId, @GoogleId)
                """;

            string? hashedPassword = null;
            if (regularRegister)
            {
                var bcrypt = new BCryptService();
                hashedPassword = bcrypt.HashPassword(request.Password!);
            }
            
            return await Connection.ExecuteAsync(sql, new
            {
                request.Nickname,
                request.Email,
                Password = hashedPassword,
                request.FirstName,
                request.LastName,
                request.PostCode,
                request.City,
                request.Consent,
                request.AppleId,
                request.GoogleId
            }) != 0;
        });

    public async Task<UserModel?> GetUserByIdAsync(int userId) =>
        await ExecuteSafelyAsync(async () =>
        {
            const string sql = "SELECT * FROM users WHERE id = @UserId;";
            var user = await Connection.QueryFirstOrDefaultAsync<UserModel>(sql, new { UserId = userId });

            if (user is null)
                return null;

            user.Password = null;
            return user;
        });
    
    public async Task<UserModel?> GetUserByEmailAsync(string email) =>
        await ExecuteSafelyAsync(async () =>
        {
            const string sql = "SELECT * FROM users WHERE email = @UserEmail";
            var user = await Connection.QueryFirstOrDefaultAsync<UserModel>(sql, new { UserEmail = email });

            if (user is null)
                return null;
            
            user.Password = null;
            return user;
        });
    
    public async Task<UserModel?> GetUserByAppleIdAsync(string appleId) =>
        await ExecuteSafelyAsync(async () =>
        {
            const string sql = "SELECT * FROM users WHERE appleid = @AppleId";
            var user = await Connection.QueryFirstOrDefaultAsync<UserModel>(sql, new { AppleId = appleId });

            if (user is null)
                return null;

            user.Password = null;
            return user;
        });

    public async Task<bool> VerifyEmailAsync(string email) =>
        await ExecuteSafelyAsync(async () =>
            await Connection.ExecuteAsync("UPDATE users SET is_email_verified = true WHERE email = @UserEmail",
                new { UserEmail = email }) != 0);

    public async Task<bool> VerifyEmailAsync(int userId) =>
        await ExecuteSafelyAsync(async () => 
            await Connection.ExecuteAsync("UPDATE users SET is_email_verified = true WHERE id = @UserId", 
                new { UserId = userId }) != 0);

    public async Task<bool> IsAdminAsync(int userId) =>
        await ExecuteSafelyAsync(async () =>
        {
            const string sql =
                """
                SELECT CASE 
                    WHEN role = 'admin' 
                        THEN TRUE
                        ELSE FALSE
                END
                FROM users
                WHERE id = @UserId
                """;
            
            return await Connection.ExecuteScalarAsync<bool>(sql, new { UserId = userId });
        });
    
    public async Task<bool> IsAdminAsync(string email) =>
        await ExecuteSafelyAsync(async () =>
        {
            const string sql =
                """
                SELECT CASE 
                    WHEN role = 'admin' 
                        THEN TRUE
                        ELSE FALSE
                END
                FROM users
                WHERE email = @Email
                """;
            
            return await Connection.ExecuteScalarAsync<bool>(sql, new { Email = email });
        });

    public async Task<bool> ChangePasswordAsync(string email, string newPassword) =>
        await ExecuteSafelyAsync(async () =>
        {
            BCryptService bcrypt = new BCryptService();
            string newHashedPassword = bcrypt.HashPassword(newPassword);

            const string sql = "UPDATE users SET password = @NewHashedPassword WHERE email = @Email";
            return await Connection.ExecuteAsync(sql,
                new
                {
                    Email = email,
                    NewHashedPassword = newHashedPassword
                }) == 1;
        });

    public async Task<bool> IsEmailVerifiedAsync(string email) =>
        await ExecuteSafelyAsync(async () =>
        {
            if (!await ExistsAsync(email))
                return false;

            const string sql = "SELECT is_email_verified FROM users WHERE email = @Email";

            return await Connection.ExecuteScalarAsync<bool>(sql, new { Email = email });
        });

    public async Task<bool> UpdateAsync(string email, UpdateUserModel model) =>
        await ExecuteSafelyAsync(async () =>
        {
            var updateFields = new List<string>();
            var parameters = new DynamicParameters();
            parameters.Add("Email", email);

            foreach (var prop in model.GetType().GetProperties().Where(p => p.GetCustomAttribute<ColumnAttribute>() is not null))
            {
                string columnName = prop.GetCustomAttribute<ColumnAttribute>()!.Name!;
                updateFields.Add($"{columnName} = @{prop.Name}");
                parameters.Add(prop.Name, prop.GetValue(model));
            }

            var sql = $"UPDATE users SET {string.Join(", ", updateFields)} WHERE email = @Email";
            Console.WriteLine(sql);
            
            return await Connection.ExecuteAsync(sql, parameters) != 0;
        });
    
    public async Task<bool> AddAppleIdAsync(string email, string appleId) =>
        await ExecuteSafelyAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(appleId))
                return false;

            const string sql = "UPDATE users SET appleid = @AppleId WHERE email = @Email";
            return await Connection.ExecuteAsync(sql, new { AppleId = appleId, Email = email }) != 0;
        });

    public async Task<bool> DeleteAsync(string email)
    {
        if (!await ExistsAsync(email))
            return false;

        return await ExecuteSafelyAsync(async () =>
        {
            const string sql = "DELETE FROM users WHERE email = @Email";
            return await Connection.ExecuteAsync(sql, new { Email = email }) != 0;
        });
    }

    public async Task<UserModel[]?> GetUsers() =>
        await ExecuteSafelyAsync(async () =>
        {
            var users = (await Connection.QueryAsync<UserModel>("SELECT * FROM users")).ToArray();
            foreach (var user in users)
                user.Password = null;
            return users;
        });

    public async Task<bool> AddGoogleIdAsync(string email, string googleId) =>
        await ExecuteSafelyAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(googleId))
                return false;

            const string sql = "UPDATE users SET google_id = @GoogleId WHERE email = @Email";
            return await Connection.ExecuteAsync(sql, new { GoogleId = googleId, Email = email }) != 0;
        });

    public async Task<UserModel?> GetUserByGoogleId(string googleId) =>
        await ExecuteSafelyAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(googleId))
                return null;
            
            const string sql = "SELECT * FROM users WHERE google_id = @GoogleId";
            return await Connection.QueryFirstOrDefaultAsync<UserModel>(sql, new { GoogleId = googleId });
        });

}