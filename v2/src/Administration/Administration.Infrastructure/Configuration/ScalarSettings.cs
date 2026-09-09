using Administration.Domain.Configuration;
using Microsoft.Extensions.Configuration;

namespace Administration.Infrastructure.Configuration;

public class ScalarSettings(IConfiguration configuration) : IScalarSettings
{
    // Optional on purpose: unset means Scalar stays Development-only (no password to check
    // against outside Development means the gate below refuses everyone, closed by default).
    public string? Password => configuration["Scalar:Password"];
}
