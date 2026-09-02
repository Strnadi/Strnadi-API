using Microsoft.Extensions.DependencyInjection;
using Strnadi.Application.Achievements;
using Strnadi.Application.Articles;
using Strnadi.Application.Auth;
using Strnadi.Application.Common;
using Strnadi.Application.Devices;
using Strnadi.Application.Maps;
using Strnadi.Application.Notifications;
using Strnadi.Application.Photos;
using Strnadi.Application.Recordings;
using Strnadi.Application.Users;

namespace Strnadi.Application.Extensions;

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
