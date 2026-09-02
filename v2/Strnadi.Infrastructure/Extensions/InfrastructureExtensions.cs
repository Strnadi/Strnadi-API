using Microsoft.Extensions.DependencyInjection;
using Strnadi.Domain.Configuration;
using Strnadi.Domain.Persistence;
using Strnadi.Domain.Persistence.Repositories;
using Strnadi.Domain.Services;
using Strnadi.Infrastructure.Auth;
using Strnadi.Infrastructure.Configuration;
using Strnadi.Infrastructure.Email;
using Strnadi.Infrastructure.MapyCz;
using Strnadi.Infrastructure.Notifications;
using Strnadi.Infrastructure.Persistence;
using Strnadi.Infrastructure.Persistence.Repositories;
using Strnadi.Infrastructure.Storage;

namespace Strnadi.Infrastructure.Extensions;

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