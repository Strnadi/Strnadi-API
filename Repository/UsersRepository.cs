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

using Auth.Models;
using Dapper;
using Microsoft.Extensions.Configuration;
using Shared.Tools;
using Shared.Models;
using Shared.Models.Database;
using Shared.Models.Requests.Users;

namespace Repository;

public class UsersRepository : RepositoryBase
{
    public UsersRepository(IConfiguration configuration) : base(configuration)
    {
    }

    public async Task<bool> ExistsAsync(string email) =>
        await ExecuteSafelyAsync(async () =>
        {
            const string sql = "SELECT COUNT(*) FROM users WHERE email = @Email";
            return await Connection.ExecuteScalarAsync<int>(sql, new { Email = email }) != 0;
        });

    public async Task<bool> IsAuthorizedAsync(string email, string password) =>
        await ExecuteSafelyAsync(async () =>
        {
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

    public async Task<bool> CreateUserAsync(SignUpRequest request) =>
        await ExecuteSafelyAsync(async () =>
        {
            const string sql =
                """
                INSERT INTO users(nickname, email, password, first_name, last_name, post_code, city, consent) 
                VALUES (@Nickname, @Email, @Password, @FirstName, @LastName, @PostCode, @City, @Consent)
                """;
            
            var bcrypt = new BCryptService();
            string hashedPassword = bcrypt.HashPassword(request.Password);
            
            return await Connection.ExecuteAsync(sql, new
            {
                request.Nickname,
                request.Email,
                Password = hashedPassword,
                request.FirstName,
                request.LastName,
                request.PostCode,
                request.City,
                request.Consent 
            }) != 0;
        });

    public async Task<UserModel?> GetUserByEmail(string email) =>
        await ExecuteSafelyAsync(async () =>
        {
            const string sql = "SELECT * FROM users WHERE email = @Email";
            var user = await Connection.QueryFirstOrDefaultAsync<UserModel>(sql, new { Email = email });

            if (user is null)
                return null;
            
            user.Password = null;
            return user;
        });

    public async Task<bool> VerifyEmailAsync(string email) =>
        await ExecuteSafelyAsync(async () =>
        {
            const string sql = "UPDATE users SET is_email_verified = true WHERE email = @Email";
            return await Connection.ExecuteAsync(sql, new { Email = email }) != 0;
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
}