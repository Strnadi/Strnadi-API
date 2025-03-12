using Repository;

class Program
{
    static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var configuration = builder.Configuration;

        ConfigureServices(builder.Services, configuration);
        
        var app = builder.Build();

        ConfigureApp(app, configuration);
        
        app.Run();
    }

    static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddMemoryCache();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
        services.AddControllers();
        services.AddRepositories();
        services.AddCors(corsOptions =>
        {
            corsOptions.AddPolicy(configuration["CORS:Default"], policyBuilder =>
            {
                policyBuilder
                    .AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            });
        });
    }
    
    static void ConfigureApp(WebApplication app, IConfiguration configuration) 
    {
        app.UseCors(configuration["CORS:Default"]);
        app.UseHttpsRedirection();
        app.UseRouting();
        app.MapControllers();
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
            options.RoutePrefix = string.Empty;
        });
    }
}