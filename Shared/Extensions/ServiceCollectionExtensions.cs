using System.Collections.Immutable;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Quartz.Impl;
using Shared.BackgroundServices.AudioProcessing;
using Shared.Tools;

namespace Shared.Extensions;

public static class ServiceCollectionExtensions
{
    public static void AddTools(this IServiceCollection services, ConfigurationManager configuration)
    {
        services.AddScoped<FirebaseNotificationService>();
        services.AddScoped<AiModelConnector>();
        services.AddSingleton<AudioProcessingQueue>();
        services.AddHostedService<AudioProcessingService>();
        
        services.AddQuartz(q =>
        {
            q.UsePersistentStore(options =>
            {
                options.UseProperties = true;
                options.UsePostgres(pg =>
                {
                    pg.ConnectionString = configuration.GetConnectionString("Quartz")
                                          ?? throw new Exception("Connection string 'Quartz' not found.");
                });
                options.UseSystemTextJsonSerializer();
            });

            q.UseDefaultThreadPool(tp => tp.MaxConcurrency = Environment.ProcessorCount);
            q.UseMicrosoftDependencyInjectionJobFactory();
        });

        services.AddQuartzHostedService(opt => opt.WaitForJobsToComplete = true);
    }}