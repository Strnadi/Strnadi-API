using Administration.Domain.Configuration;
using Administration.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Administration.Infrastructure.Persistence;

public class AdminDbContextFactory : IDesignTimeDbContextFactory<AdminDbContext>
{
    public AdminDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("Default")
            ?? "Host=localhost;Port=5432;Database=administration;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<AdminDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AdminDbContext(options, new AesEncryptionService(new DesignTimeEncryptionSettings(configuration)));
    }

    private sealed class DesignTimeEncryptionSettings(IConfiguration configuration) : IEncryptionSettings
    {
        public string Key => configuration["Encryption:Key"] ?? Convert.ToBase64String(new byte[32]);
    }
}
