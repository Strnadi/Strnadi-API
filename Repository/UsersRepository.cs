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
            const string sql = """SELECT COUNT(*) FROM "Users" WHERE "Email" = @Email""";
            return await Connection.ExecuteScalarAsync<int>(sql, new { Email = email }) != 0;
        });
    }
    
    public async Task<bool> IsAuthorizedAsync(string email, string password)
    {
        return await ExecuteSafelyAsync(async () =>
        {
            const string sql = """SELECT "Password" FROM "Users" WHERE "Email" = @Email""";

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
                INSERT INTO "Users" ("Nickname", "Email", "Password", "FirstName", "LastName", "Consent") 
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
            const string sql = """SELECT * FROM "Users" WHERE "Email" = @Email""";
            return await Connection.QueryFirstOrDefaultAsync<UserModel>(sql, new { Email = email });
        });
    }

    public async Task<bool> VerifyEmailAsync(string email)
    {
        return await ExecuteSafelyAsync(async () =>
        {
            const string sql = """UPDATE "Users" SET "IsEmailVerified" = true WHERE "Email" = @Email""";
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
                    WHEN "UserRole" = 'admin' 
                        THEN TRUE
                        ELSE FALSE
                FROM "Users"
                WHERE "Email" = @Email
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

            const string sql = """UPDATE "Users" SET "Password" = @NewHashedPasswod WHERE "Email" = @Email""";
            return await Connection.ExecuteAsync(sql,
                       new
                       {
                           Email = email,
                           NewHashedPassword = newHashedPassword
                       }) == 1;
        });
    }
}