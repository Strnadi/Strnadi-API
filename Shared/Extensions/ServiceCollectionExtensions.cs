using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Tools;

namespace Shared.Extensions;

public static class ServiceCollectionExtensions
{
    public static void AddTools(this IServiceCollection services)
    {
        services.AddScoped<FirebaseNotificationService>();
    }
}