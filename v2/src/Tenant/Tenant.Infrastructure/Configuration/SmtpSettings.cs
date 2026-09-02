using Microsoft.Extensions.Configuration;
using Tenant.Domain.Configuration;

namespace Tenant.Infrastructure.Configuration;

public class SmtpSettings(IConfiguration configuration) : ISmtpSettings
{
    public string Domain => configuration["Smtp:Domain"]
        ?? throw new InvalidOperationException("Smtp:Domain is not configured");

    public string Username => configuration["Smtp:Username"]
        ?? throw new InvalidOperationException("Smtp:Username is not configured");

    public string Password => configuration["Smtp:Password"]
        ?? throw new InvalidOperationException("Smtp:Password is not configured");

    public ushort Port => ushort.Parse(configuration["Smtp:Port"]
        ?? throw new InvalidOperationException("Smtp:Port is not configured"));

    public string Email => configuration["Smtp:Email"]
        ?? throw new InvalidOperationException("Smtp:Email is not configured");

    public bool EnableSsl => bool.Parse(configuration["Smtp:EnableSsl"]
        ?? throw new InvalidOperationException("Smtp:EnableSsl is not configured"));
}
