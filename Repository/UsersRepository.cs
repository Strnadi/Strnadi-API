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
using Microsoft.Extensions.Configuration;
using Shared.Tools;
using Shared.Models;

namespace Repository;

public class UsersRepository : RepositoryBase
{
    public UsersRepository(IConfiguration configuration) : base(configuration)
    {
    }

    public async Task<bool> ExistsAsync(string email)
    {
        return await ExecuteSafelyAsync(async () =>
        {
            const string sql = "SELECT COUNT(*) FROM users WHERE email = @Email";
            return await Connection.ExecuteScalarAsync<int>(sql, new { Email = email }) != 0;
        });
    }
    
    public async Task<bool> IsAuthorizedAsync(string email, string password)
    {
        return await ExecuteSafelyAsync(async () =>
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
    }

    public async Task<bool> CreateUserAsync(string? nickname,
        string email,
        string password,
        string firstName,
        string lastName, 
        bool consent)
    {
        return await ExecuteSafelyAsync(async () =>
        {
            const string sql =
                """
                INSERT INTO users(nickname, email, password, first_name, last_name, consent) 
                VALUES (@Nickname, @Email, @Password, @FirstName, @LastName, @Consent)
                """;
            
            var bcrypt = new BCryptService();
            string hashedPassword = bcrypt.HashPassword(password);
            
            return await Connection.ExecuteAsync(sql, new
            {
                Nickname = nickname,
                Email = email,
                Password = hashedPassword,
                FirstName = firstName,
                LastName = lastName,
                Consent = consent 
            }) != 0;
        });
    }

    public async Task<UserModel?> GetUserByEmail(string email)
    {
        return await ExecuteSafelyAsync(async () =>
        {
            const string sql = "SELECT * FROM users WHERE email = @Email";
            return await Connection.QueryFirstOrDefaultAsync<UserModel>(sql, new { Email = email });
        });
    }

    public async Task<bool> VerifyEmailAsync(string email)
    {
        return await ExecuteSafelyAsync(async () =>
        {
            const string sql = "UPDATE users SET is_email_verified = true WHERE email = @Email";
            return await Connection.ExecuteAsync(sql, new { Email = email }) != 0;
        });
    }

    public async Task<bool> IsAdminAsync(string email)
    {
        return await ExecuteSafelyAsync(async () =>
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
    }

    public async Task<bool> ChangePasswordAsync(string email, string newPassword)
    {
        return await ExecuteSafelyAsync(async () =>
        {
            BCryptService bcrypt = new BCryptService();
            string newHashedPassword = bcrypt.HashPassword(newPassword);

            const string sql = "UPDATE users SET password = @NewHashedPasswod WHERE email = @Email";
            return await Connection.ExecuteAsync(sql,
                       new
                       {
                           Email = email,
                           NewHashedPassword = newHashedPassword
                       }) == 1;
        });
    }
}