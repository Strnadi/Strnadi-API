using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Tenant.Domain.Configuration;
using Tenant.Infrastructure.Security;

namespace Tenant.Infrastructure.Persistence;

public class TenantDbContextFactory : IDesignTimeDbContextFactory<TenantDbContext>
{
    public TenantDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("Default")
            ?? "Host=localhost;Port=5432;Database=strnadi;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new TenantDbContext(options, new AesEncryptionService(new DesignTimeEncryptionSettings(configuration)));
    }

    private sealed class DesignTimeEncryptionSettings(IConfiguration configuration) : IEncryptionSettings
    {
        public string Key => configuration["Encryption:Key"] ?? Convert.ToBase64String(new byte[32]);
    }
}
