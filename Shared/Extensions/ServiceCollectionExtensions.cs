using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Quartz.Serialization.SystemTextJson;
using Shared.Tools;

namespace Shared.Extensions;

public static class ServiceCollectionExtensions
{
    public static void AddTools(this IServiceCollection services, ConfigurationManager configuration)
    {
        
        services.AddScoped<FirebaseNotificationService>();
        services.AddQuartz(q =>
        {
            q.UsePersistentStore(options =>
            {
                options.UseProperties = true;
                options.UsePostgres(pg =>
                {
                    pg.ConnectionString = configuration.GetConnectionString("Default") ??
                                          throw new Exception("Connection string 'Default' not found.");
                });
            });
            q.UseDefaultThreadPool(tp => tp.MaxConcurrency = Environment.ProcessorCount);
            q.SetProperty("quartz.serializer.type", "json");
        });
        services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);
    }
}