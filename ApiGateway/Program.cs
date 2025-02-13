using ApiGateway.Services;
using DotNetEnv;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

Env.Load();

builder.Configuration.AddEnvironmentVariables();

Console.WriteLine(configuration["JWT_SECRET_KEY"]);

builder.Services.AddControllers();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddCors(corsOptions =>
{
    corsOptions.AddPolicy(configuration["CORS_DEFAULT"], policyBuilder =>
    {
        policyBuilder
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var app = builder.Build();

app.MapGet("/neco", () => "Hello World!");

app.UseHttpsRedirection();
app.UseRouting();
app.MapControllers();
app.Run();