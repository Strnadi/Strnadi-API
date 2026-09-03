using Administration.Domain.Configuration;
using Administration.Domain.Persistence;
using Administration.Domain.Persistence.Repositories;
using Administration.Domain.Services;
using Administration.Infrastructure.Auth;
using Administration.Infrastructure.Configuration;
using Administration.Infrastructure.Persistence;
using Administration.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Administration.Infrastructure.Extensions;

public static class InfrastructureExtensions
{
    extension(IServiceCollection serviceCollection)
    {
        public IServiceCollection AddInfrastructure()
        {
            serviceCollection.AddSingleton<IGoogleAuthSettings, GoogleAuthSettings>();
            serviceCollection.AddSingleton<IAppleAuthSettings, AppleAuthSettings>();

            serviceCollection.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
            serviceCollection.AddScoped<IGoogleIdTokenValidator, GoogleIdTokenValidator>();
            serviceCollection.AddSingleton<IAppleIdTokenValidator, AppleIdTokenValidator>();

            serviceCollection.AddScoped<IUnitOfWork, UnitOfWork>();
            serviceCollection.AddScoped<IUsersRepository, UsersRepository>();
            serviceCollection.AddScoped<IRolesRepository, RolesRepository>();

            return serviceCollection;
        }
    }
}