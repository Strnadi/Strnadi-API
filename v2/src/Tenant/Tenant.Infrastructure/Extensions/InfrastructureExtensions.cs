using Microsoft.Extensions.DependencyInjection;
using Tenant.Domain.Configuration;
using Tenant.Domain.Persistence;
using Tenant.Domain.Persistence.Repositories;
using Tenant.Domain.Services;
using Tenant.Infrastructure.Auth;
using Tenant.Infrastructure.Configuration;
using Tenant.Infrastructure.Email;
using Tenant.Infrastructure.MapyCz;
using Tenant.Infrastructure.Notifications;
using Tenant.Infrastructure.Persistence;
using Tenant.Infrastructure.Persistence.Repositories;
using Tenant.Infrastructure.Storage;

namespace Tenant.Infrastructure.Extensions;

public static class InfrastructureExtensions
{
    extension(IServiceCollection serviceCollection)
    {
        public IServiceCollection AddInfrastructure()
        {
            serviceCollection.AddSingleton<IJwtSettings, JwtSettings>();
            serviceCollection.AddSingleton<IHostSettings, HostSettings>();
            serviceCollection.AddSingleton<IDatabaseSettings, DatabaseSettings>();
            serviceCollection.AddSingleton<ICorsSettings, CorsSettings>();
            serviceCollection.AddSingleton<IFirebaseSettings, FirebaseSettings>();
            serviceCollection.AddSingleton<IMapyCzSettings, MapyCzSettings>();
            serviceCollection.AddSingleton<ISmtpSettings, SmtpSettings>();
            serviceCollection.AddSingleton<IGoogleAuthSettings, GoogleAuthSettings>();
            serviceCollection.AddSingleton<IAppleAuthSettings, AppleAuthSettings>();
            serviceCollection.AddSingleton<IAuthSettings, AuthSettings>();
            serviceCollection.AddSingleton<IFileStorageSettings, LocalStorageSettings>();
            
            serviceCollection.AddSingleton<IFileStorage, LocalStorage>();

            serviceCollection.AddScoped<IUnitOfWork, UnitOfWork>();

            serviceCollection.AddScoped<IUsersRepository, UsersRepository>();
            serviceCollection.AddScoped<IPhotosRepository, PhotosRepository>();
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

            serviceCollection.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
            serviceCollection.AddScoped<IGoogleIdTokenValidator, GoogleIdTokenValidator>();
            serviceCollection.AddSingleton<IAppleIdTokenValidator, AppleIdTokenValidator>();
            serviceCollection.AddScoped<IEmailSender, SmtpEmailSender>();

            serviceCollection.AddHttpClient<IMapyCzProxyService, MapyCzProxyService>();
            serviceCollection.AddHttpClient<IPushNotificationService, FirebaseNotificationService>();

            return serviceCollection;
        }
    }
}