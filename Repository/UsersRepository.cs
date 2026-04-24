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

/// <summary>
/// Provides database access for user accounts, credentials, verification state, and external provider identifiers.
/// </summary>
public class UsersRepository : RepositoryBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UsersRepository"/> class.
    /// </summary>
    /// <param name="configuration">Application configuration used by the repository base.</param>
    public UsersRepository(IConfiguration configuration) : base(configuration)
    {
    }
    
    /// <summary>
    /// Checks whether a user exists with the specified email address.
    /// </summary>
    /// <param name="email">Email address to check.</param>
    /// <returns><see langword="true"/> when a matching user exists; otherwise, <see langword="false"/>.</returns>
    public async Task<bool> ExistsAsync(string email) =>
        await ExecuteSafelyAsync(async () => 
            await Connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM users WHERE email = @Email", 
                new { Email = email }
                )) != 0;
    
    /// <summary>
    /// Checks whether a user exists with the specified Apple identifier.
    /// </summary>
    /// <param name="appleId">Apple identifier to check.</param>
    /// <returns><see langword="true"/> when a matching user exists; otherwise, <see langword="false"/>.</returns>
    public async Task<bool> ExistsAppleAsync(string appleId) =>
        await ExecuteSafelyAsync(async () => 
            await Connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM users WHERE appleid = @AppleId", 
                new { AppleId = appleId }
                )) != 0;

    /// <summary>
    /// Checks whether a user exists with the specified user identifier.
    /// </summary>
    /// <param name="userId">User identifier to check.</param>
    /// <returns><see langword="true"/> when a matching user exists; otherwise, <see langword="false"/>.</returns>
    public async Task<bool> ExistsAsync(int userId) =>
        await ExecuteSafelyAsync(async () =>
            await Connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM users WHERE id = @Id",
                new { Id = userId }
            )) != 0;
    
    /// <summary>
    /// Checks whether a user exists with the specified Google identifier.
    /// </summary>
    /// <param name="googleId">Google identifier to check.</param>
    /// <returns><see langword="true"/> when a matching user exists; otherwise, <see langword="false"/>.</returns>
    public async Task<bool> ExistsGoogleAsync(string googleId) => 
        await ExecuteSafelyAsync(async () => 
            await Connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM users WHERE google_id = @GoogleId",
                new { GoogleId = googleId }
            )) != 0;
    

    /// <summary>
    /// Verifies email and password credentials for a user.
    /// </summary>
    /// <param name="email">Email address of the user to authorize.</param>
    /// <param name="password">Plain-text password to verify against the stored hash.</param>
    /// <returns><see langword="true"/> when the credentials are valid; otherwise, <see langword="false"/>.</returns>
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

    /// <summary>
    /// Creates a new user from the supplied sign-up request.
    /// </summary>
    /// <param name="request">Sign-up request containing user profile and provider identifiers.</param>
    /// <param name="regularRegister">Whether to hash and store the supplied password for a regular registration.</param>
    /// <returns><see langword="true"/> when the user row is inserted; otherwise, <see langword="false"/>.</returns>
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

    /// <summary>
    /// Gets a user by database identifier.
    /// </summary>
    /// <param name="userId">User identifier to load.</param>
    /// <returns>The matching user without the password value, or <see langword="null"/> when not found.</returns>
    public async Task<User?> GetUserByIdAsync(int userId) =>
        await ExecuteSafelyAsync(async () =>
        {
            const string sql = "SELECT * FROM users WHERE id = @UserId;";
            var user = await Connection.QueryFirstOrDefaultAsync<User>(sql, new { UserId = userId });

            if (user is null)
                return null;

            user.Password = null;
            return user;
        });
    
    /// <summary>
    /// Gets a user by email address.
    /// </summary>
    /// <param name="email">Email address to load.</param>
    /// <returns>The matching user without the password value, or <see langword="null"/> when not found.</returns>
    public async Task<User?> GetUserByEmailAsync(string email) =>
        await ExecuteSafelyAsync(async () =>
        {
            const string sql = "SELECT * FROM users WHERE email = @UserEmail";
            var user = await Connection.QueryFirstOrDefaultAsync<User>(sql, new { UserEmail = email });

            if (user is null)
                return null;
            
            user.Password = null;
            return user;
        });
    
    /// <summary>
    /// Gets a user by Apple identifier.
    /// </summary>
    /// <param name="appleId">Apple identifier to load.</param>
    /// <returns>The matching user without the password value, or <see langword="null"/> when not found.</returns>
    public async Task<User?> GetUserByAppleIdAsync(string appleId) =>
        await ExecuteSafelyAsync(async () =>
        {
            const string sql = "SELECT * FROM users WHERE appleid = @AppleId";
            var user = await Connection.QueryFirstOrDefaultAsync<User>(sql, new { AppleId = appleId });

            if (user is null)
                return null;

            user.Password = null;
            return user;
        });

    /// <summary>
    /// Marks the user with the specified email address as email verified.
    /// </summary>
    /// <param name="email">Email address of the user to verify.</param>
    /// <returns><see langword="true"/> when a user row is updated; otherwise, <see langword="false"/>.</returns>
    public async Task<bool> VerifyEmailAsync(string email) =>
        await ExecuteSafelyAsync(async () =>
            await Connection.ExecuteAsync("UPDATE users SET is_email_verified = true WHERE email = @UserEmail",
                new { UserEmail = email }) != 0);

    /// <summary>
    /// Marks the user with the specified identifier as email verified.
    /// </summary>
    /// <param name="userId">Identifier of the user to verify.</param>
    /// <returns><see langword="true"/> when a user row is updated; otherwise, <see langword="false"/>.</returns>
    public async Task<bool> VerifyEmailAsync(int userId) =>
        await ExecuteSafelyAsync(async () => 
            await Connection.ExecuteAsync("UPDATE users SET is_email_verified = true WHERE id = @UserId", 
                new { UserId = userId }) != 0);

    /// <summary>
    /// Checks whether the user with the specified identifier has the administrator role.
    /// </summary>
    /// <param name="userId">Identifier of the user to inspect.</param>
    /// <returns><see langword="true"/> when the user has the administrator role; otherwise, <see langword="false"/>.</returns>
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
    
    /// <summary>
    /// Checks whether the user with the specified email address has the administrator role.
    /// </summary>
    /// <param name="email">Email address of the user to inspect.</param>
    /// <returns><see langword="true"/> when the user has the administrator role; otherwise, <see langword="false"/>.</returns>
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

    /// <summary>
    /// Changes the stored password for the specified user.
    /// </summary>
    /// <param name="email">Email address of the user whose password should be changed.</param>
    /// <param name="newPassword">New plain-text password to hash and store.</param>
    /// <returns><see langword="true"/> when exactly one user row is updated; otherwise, <see langword="false"/>.</returns>
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

    /// <summary>
    /// Checks whether the user with the specified email address has a verified email.
    /// </summary>
    /// <param name="email">Email address of the user to inspect.</param>
    /// <returns><see langword="true"/> when the user exists and has a verified email; otherwise, <see langword="false"/>.</returns>
    public async Task<bool> IsEmailVerifiedAsync(string email) =>
        await ExecuteSafelyAsync(async () =>
        {
            if (!await ExistsAsync(email))
                return false;

            const string sql = "SELECT is_email_verified FROM users WHERE email = @Email";

            return await Connection.ExecuteScalarAsync<bool>(sql, new { Email = email });
        });

    /// <summary>
    /// Updates mapped user fields for the user with the specified email address.
    /// </summary>
    /// <param name="email">Email address of the user to update.</param>
    /// <param name="model">Model containing fields decorated with database column names.</param>
    /// <returns><see langword="true"/> when at least one user row is updated; otherwise, <see langword="false"/>.</returns>
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
    
    /// <summary>
    /// Adds or replaces the Apple identifier for a user.
    /// </summary>
    /// <param name="email">Email address of the user to update.</param>
    /// <param name="appleId">Apple identifier to store.</param>
    /// <returns><see langword="true"/> when a non-empty Apple identifier is stored; otherwise, <see langword="false"/>.</returns>
    public async Task<bool> AddAppleIdAsync(string email, string appleId) =>
        await ExecuteSafelyAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(appleId))
                return false;

            const string sql = "UPDATE users SET appleid = @AppleId WHERE email = @Email";
            return await Connection.ExecuteAsync(sql, new { AppleId = appleId, Email = email }) != 0;
        });

    /// <summary>
    /// Deletes the user with the specified email address.
    /// </summary>
    /// <param name="email">Email address of the user to delete.</param>
    /// <returns><see langword="true"/> when the user existed and was deleted; otherwise, <see langword="false"/>.</returns>
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

    /// <summary>
    /// Gets all users.
    /// </summary>
    /// <returns>An array of users without password values, or <see langword="null"/> when the query fails.</returns>
    public async Task<User[]?> GetUsers() =>
        await ExecuteSafelyAsync(async () =>
        {
            var users = (await Connection.QueryAsync<User>("SELECT * FROM users")).ToArray();
            foreach (var user in users)
                user.Password = null;
            return users;
        });

    /// <summary>
    /// Adds or replaces the Google identifier for a user and marks the email as verified.
    /// </summary>
    /// <param name="email">Email address of the user to update.</param>
    /// <param name="googleId">Google identifier to store.</param>
    /// <returns><see langword="true"/> when a non-empty Google identifier is stored; otherwise, <see langword="false"/>.</returns>
    public async Task<bool> AddGoogleIdAsync(string email, string googleId) =>
        await ExecuteSafelyAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(googleId))
                return false;

            const string sql = "UPDATE users SET google_id = @GoogleId, is_email_verified = true WHERE email = @Email";
            return await Connection.ExecuteAsync(sql, new { GoogleId = googleId, Email = email }) != 0;
        });

    /// <summary>
    /// Gets a user by Google identifier.
    /// </summary>
    /// <param name="googleId">Google identifier to load.</param>
    /// <returns>The matching user, or <see langword="null"/> when the identifier is blank or no user is found.</returns>
    public async Task<User?> GetUserByGoogleId(string googleId) =>
        await ExecuteSafelyAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(googleId))
                return null;
            
            const string sql = "SELECT * FROM users WHERE google_id = @GoogleId";
            return await Connection.QueryFirstOrDefaultAsync<User>(sql, new { GoogleId = googleId });
        });

}
