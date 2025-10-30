using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
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
                    pg.ConnectionString = configuration.GetConnectionString("Quartz") ??
                                          throw new Exception("Connection string 'Quartz' not found.");
                });
                options.UseSystemTextJsonSerializer();
            });
            q.UseDefaultThreadPool(tp => tp.MaxConcurrency = Environment.ProcessorCount);
        });
        services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);
    }
}