using Administration.Domain.Configuration;
using Administration.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Administration.Infrastructure.Persistence;

// EF Core's design-time tooling (`dotnet ef migrations add`/`database update`) discovers this
// factory automatically and uses it instead of booting the full app through Program.cs. That
// matters here: booting the app would also run Program.cs's own startup code
// (SyncOpenIddictClientAsync, which queries the `projects` table - one that may not exist yet on
// a fresh database) and would require every other setting to already be configured.
public sealed class AdminDbContextFactory : IDesignTimeDbContextFactory<AdminDbContext>
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

    // Falls back to a throwaway key when Encryption:Key isn't configured - fine for schema
    // generation, which never actually encrypts/decrypts a real value.
    private sealed class DesignTimeEncryptionSettings(IConfiguration configuration) : IEncryptionSettings
    {
        public string Key => configuration["Encryption:Key"] ?? Convert.ToBase64String(new byte[32]);
    }
}
