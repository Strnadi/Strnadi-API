using Administration.Infrastructure.Configuration;
using Administration.Infrastructure.Persistence;
using Administration.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Administration.Api;

// EF tooling must not start OAuth, load certificates, or seed users to build a bundle.
public sealed class AdminDbContextFactory : IDesignTimeDbContextFactory<AdminDbContext>
{
    public AdminDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        var connection = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is required.");
        var options = new DbContextOptionsBuilder<AdminDbContext>()
            .UseNpgsql(connection)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new AdminDbContext(options, new AesEncryptionService(new EncryptionSettings(configuration)));
    }
}
