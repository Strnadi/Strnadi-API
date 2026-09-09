using Administration.Domain.Configuration;
using Microsoft.Extensions.Configuration;

namespace Administration.Infrastructure.Configuration;

public class ScalarSettings(IConfiguration configuration) : IScalarSettings
{
    // Both optional on purpose: unset means the gate refuses everyone outside Development,
    // closed by default, rather than accepting any username/blank credentials.
    public string? Username => configuration["Scalar:Username"];

    public string? Password => configuration["Scalar:Password"];
}
