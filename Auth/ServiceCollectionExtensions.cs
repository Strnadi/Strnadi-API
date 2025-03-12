using Auth.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Auth;

public static class ServiceCollectionExtensions
{
    public static void AddAuthServices(this IServiceCollection services)
    {
        services.AddScoped<JwtService>();
    }
}