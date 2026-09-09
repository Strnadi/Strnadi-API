using Administration.Domain.Configuration;
using Microsoft.Extensions.Configuration;

namespace Administration.Infrastructure.Configuration;

public class ScalarSettings(IConfiguration configuration) : IScalarSettings
{
    public string? Username => configuration["Scalar:Username"];

    public string? Password => configuration["Scalar:Password"];
}
