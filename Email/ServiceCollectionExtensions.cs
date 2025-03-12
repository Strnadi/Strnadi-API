using Microsoft.Extensions.DependencyInjection;

namespace Email;

public static class ServiceCollectionExtensions 
{
    public static void AddEmailServices(this IServiceCollection services)
    {
        services.AddSingleton<EmailService>();
        services.AddSingleton<EmailSender>();
        services.AddSingleton<LinkGenerator>();
    }
}