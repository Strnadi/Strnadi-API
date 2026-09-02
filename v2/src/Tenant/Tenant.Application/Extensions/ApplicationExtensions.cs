using Microsoft.Extensions.DependencyInjection;
using Tenant.Application.Achievements;
using Tenant.Application.Articles;
using Tenant.Application.Auth;
using Tenant.Application.Common;
using Tenant.Application.Devices;
using Tenant.Application.Maps;
using Tenant.Application.Notifications;
using Tenant.Application.Photos;
using Tenant.Application.Recordings;
using Tenant.Application.Users;

namespace Tenant.Application.Extensions;

public static class ApplicationExtensions
{
    extension(IServiceCollection serviceCollection)
    {
        public IServiceCollection AddApplication()
        {
            serviceCollection.AddScoped<UsersService>();
            serviceCollection.AddScoped<PhotosService>();
            serviceCollection.AddScoped<NotificationsService>();
            serviceCollection.AddScoped<MapClustersService>();
            serviceCollection.AddScoped<DevicesService>();
            serviceCollection.AddScoped<AchievementsService>();
            serviceCollection.AddScoped<DetectedDialectsService>();
            serviceCollection.AddScoped<ArticlesService>();
            serviceCollection.AddScoped<ArticleCategoriesService>();
            serviceCollection.AddScoped<RecordingsService>();
            serviceCollection.AddScoped<RecordingPartsService>();
            serviceCollection.AddScoped<FilteredRecordingPartsService>();
            serviceCollection.AddScoped<AuthService>();
            serviceCollection.AddScoped<LinkBuilder>();

            return serviceCollection;
        }
    }
}
