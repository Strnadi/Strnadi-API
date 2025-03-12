using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Repository;

public static class ServiceCollectionExtensions
{
    public static void AddRepositories(this IServiceCollection services)
    {
        Assembly currentAssembly = Assembly.GetExecutingAssembly();
        var repoBaseType = typeof(RepositoryBase);
        var repoTypes = currentAssembly.GetTypes().Where(t => t != repoBaseType && t.IsAssignableTo(repoBaseType));

        foreach (Type repoType in repoTypes)
        {
            services.AddScoped(repoType);
        }
    }
}