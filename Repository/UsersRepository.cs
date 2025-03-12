using Dapper;
using Microsoft.Extensions.Configuration;
using Models.Requests;
using Shared.Tools;

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
        string lastName)
    {
        return await ExecuteSafelyAsync(async () =>
        {
            const string sql =
                """
                INSERT INTO "Users" ("Nickname", "Email", "Password", "FirstName", "LastName") 
                VALUES (@Nickname, @Email, @Password, @FirstName, @LastName)
                """;
            
            var bcrypt = new BCryptService();
            string hashedPassword = bcrypt.HashPassword(password);
            
            return await Connection.ExecuteAsync(sql, new
            {
                Nickname = nickname,
                Email = email,
                Password = hashedPassword,
                FirstName = firstName,
                LastName = lastName
            }) != 0;
        });
    }
}