using Microsoft.Extensions.DependencyInjection;
using Tenant.Domain.Configuration;
using Tenant.Domain.Persistence;
using Tenant.Domain.Persistence.Repositories;
using Tenant.Domain.Services;
using Tenant.Infrastructure.Ai;
using Tenant.Infrastructure.Audio;
using Tenant.Infrastructure.AudioProcessing;
using Tenant.Infrastructure.Configuration;
using Tenant.Infrastructure.MapyCz;
using Tenant.Infrastructure.Notifications;
using Tenant.Infrastructure.Persistence;
using Tenant.Infrastructure.Persistence.Repositories;
using Tenant.Infrastructure.Security;
using Tenant.Infrastructure.Storage;

namespace Tenant.Infrastructure.Extensions;

public static class InfrastructureExtensions
{
    extension(IServiceCollection serviceCollection)
    {
        public IServiceCollection AddInfrastructure()
        {
            serviceCollection.AddSingleton<IProjectSettings, ProjectSettings>();
            serviceCollection.AddSingleton<IHostSettings, HostSettings>();
            serviceCollection.AddSingleton<IDatabaseSettings, DatabaseSettings>();
            serviceCollection.AddSingleton<ICorsSettings, CorsSettings>();
            serviceCollection.AddSingleton<IFirebaseSettings, FirebaseSettings>();
            serviceCollection.AddSingleton<IMapyCzSettings, MapyCzSettings>();
            serviceCollection.AddSingleton<IFileStorageSettings, LocalStorageSettings>();
            serviceCollection.AddSingleton<IEncryptionSettings, EncryptionSettings>();

            serviceCollection.AddSingleton<IFileStorage, LocalStorage>();
            // Singleton is required, not just convenient: TenantDbContext bakes the converter
            // instance into the EF model, which EF builds once and caches for the app's lifetime.
            // A Scoped/Transient instance captured there would silently pin whichever request
            // happened to trigger the first model build.
            serviceCollection.AddSingleton<IEncryptionService, AesEncryptionService>();

            serviceCollection.AddScoped<IUnitOfWork, UnitOfWork>();

            serviceCollection.AddScoped<IDialectsRepository, DialectsRepository>();
            serviceCollection.AddScoped<IRecordingsRepository, RecordingsRepository>();
            serviceCollection.AddScoped<IRecordingPartsRepository, RecordingPartsRepository>();
            serviceCollection.AddScoped<IFilteredRecordingPartsRepository, FilteredRecordingPartsRepository>();
            serviceCollection.AddScoped<IDetectedDialectsRepository, DetectedDialectsRepository>();
            serviceCollection.AddScoped<IDevicesRepository, DevicesRepository>();
            serviceCollection.AddScoped<IAchievementsRepository, AchievementsRepository>();
            serviceCollection.AddScoped<IArticlesRepository, ArticlesRepository>();
            serviceCollection.AddScoped<IArticleCategoriesRepository, ArticleCategoriesRepository>();
            serviceCollection.AddScoped<IMapPointsRepository, MapPointsRepository>();

            serviceCollection.AddHttpClient<IMapyCzProxyService, MapyCzProxyService>();
            serviceCollection.AddHttpClient<IPushNotificationService, FirebaseNotificationService>();

            serviceCollection.AddSingleton<IClassificationSettings, ClassificationSettings>();
            serviceCollection.AddSingleton<IAudioNormalizer, FFmpegAudioNormalizer>();
            serviceCollection.AddHttpClient<IDialectClassifier, AiModelDialectClassifier>((sp, client) =>
                client.BaseAddress = new Uri(sp.GetRequiredService<IClassificationSettings>().BaseUrl));

            serviceCollection.AddSingleton<ClassificationQueue>();
            serviceCollection.AddSingleton<IClassificationQueue>(sp => sp.GetRequiredService<ClassificationQueue>());
            serviceCollection.AddHostedService<ClassificationBackgroundService>();

            return serviceCollection;
        }
    }
}