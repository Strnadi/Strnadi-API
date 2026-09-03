using Administration.Application.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace Administration.Application.Extensions;

public static class ApplicationExtensions
{
    extension(IServiceCollection serviceCollection)
    {
        public IServiceCollection AddApplication()
        {
            serviceCollection.AddScoped<AuthService>();

            return serviceCollection;
        }
    }
}