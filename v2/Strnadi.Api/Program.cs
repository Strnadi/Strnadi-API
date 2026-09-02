using System.Text;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Console;
using Microsoft.IdentityModel.Tokens;

using Strnadi.Api.ExceptionHandling;
using Strnadi.Api.Logging;
using Strnadi.Application.Extensions;
using Strnadi.Domain.Configuration;
using Strnadi.Domain.Services;
using Strnadi.Infrastructure.Auth;
using Strnadi.Infrastructure.Extensions;
using Strnadi.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddInfrastructure();
builder.Services.AddApplication();

builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    options.UseNpgsql(sp.GetRequiredService<IDatabaseSettings>().ConnectionString));

builder.Services.AddOpenApi();

builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IJwtSettings>((options, jwt) =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey)),
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", policy => policy.RequireClaim("role", "admin"));

builder.Services.AddCors();
builder.Services.AddOptions<CorsOptions>()
    .Configure<ICorsSettings>((options, cors) =>
    {
        options.AddPolicy(cors.Default, policy => 
            policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
    });

builder.Logging.AddConsole(options => options.FormatterName = "compact");
builder.Services.AddSingleton<ConsoleFormatter, CompactConsoleFormatter>();

var app = builder.Build();

app.UseHttpsRedirection();

app.UseCors(app.Services.GetRequiredService<ICorsSettings>().Default);

app.UseExceptionHandler();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
