using System.Text;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Console;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ServiceDefaults;
using Tenant.Api.ExceptionHandling;
using Tenant.Api.Logging;
using Tenant.Application.Extensions;
using Tenant.Domain.Configuration;
using Tenant.Domain.Services;
using Tenant.Infrastructure.Auth;
using Tenant.Infrastructure.Extensions;
using Tenant.Infrastructure.Persistence;

LoadEnvFile(Path.Combine(AppContext.BaseDirectory, ".env.development"));

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddControllers();

builder.Services.AddInfrastructure();
builder.Services.AddApplication();

builder.Services.AddDbContext<TenantDbContext>((sp, options) =>
    options.UseNpgsql(sp.GetRequiredService<IDatabaseSettings>().ConnectionString));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<TenantDbContext>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Strnadi API",
        Version = "v1"
    });

    options.AddSecurityDefinition("bearerAuth", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "bearerAuth" }
            },
            []
        }
    });

    foreach (var xmlFile in Directory.GetFiles(AppContext.BaseDirectory, "Tenant.*.xml"))
    {
        options.IncludeXmlComments(xmlFile, includeControllerXmlComments: true);
    }
});

builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IJwtSettings>((options, jwt) =>
    {
        // Keep claim types as issued ("sub", "role", ...) instead of the default remap to
        // long ClaimTypes.* URIs — GetCallerId()/IsAdmin() and the AdminOnly policy all look
        // up claims by their original short JWT names.
        options.MapInboundClaims = false;
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

builder.Services.AddSingleton<ConsoleFormatter, CompactConsoleFormatter>();
builder.Logging.AddConsole(options => options.FormatterName = "compact");

var app = builder.Build();

// Migrations are applied manually (`dotnet ef database update`), not on startup — Aspire can
// point this at a shared/copied database, and auto-migrating on every boot would race against
// other instances or mutate a database other people are using.
app.MapDefaultEndpoints();

app.UseHttpsRedirection();

app.UseCors(app.Services.GetRequiredService<ICorsSettings>().Default);

app.UseExceptionHandler();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/utils/health");

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Strnadi API v2");
    options.RoutePrefix = "swagger";
    options.DocumentTitle = "Strnadi API - Swagger";
});

app.Run();

// Loads KEY=VALUE pairs from a .env file into the process environment so
// ASP.NET Core's built-in environment-variable configuration provider picks
// them up (Jwt__SecretKey -> config key "Jwt:SecretKey"). Variables already
// set in the environment take precedence over the file.
static void LoadEnvFile(string path)
{
    if (!File.Exists(path))
        return;

    foreach (var line in File.ReadAllLines(path))
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            continue;

        var separatorIndex = trimmed.IndexOf('=');
        if (separatorIndex < 0)
            continue;

        var key = trimmed[..separatorIndex].Trim();
        var value = trimmed[(separatorIndex + 1)..].Trim().Trim('"');

        if (Environment.GetEnvironmentVariable(key) is null)
            Environment.SetEnvironmentVariable(key, value);
    }
}
