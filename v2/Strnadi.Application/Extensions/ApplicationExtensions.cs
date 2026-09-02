using Microsoft.Extensions.DependencyInjection;
using Strnadi.Application.Devices;
using Strnadi.Application.Maps;
using Strnadi.Application.Notifications;
using Strnadi.Application.Photos;
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

            return serviceCollection;
        }
    }
}
