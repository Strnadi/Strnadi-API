using ApiGateway.Services;
using DotNetEnv;

var builder = WebApplication.CreateBuilder(args);

Env.Load("../.env");

builder.Configuration.AddEnvironmentVariables();

builder.Services.AddControllers();
builder.Services.AddScoped<IJwtService, JwtService>();

var app = builder.Build();

app.Run();