using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Console;
using Microsoft.OpenApi.Models;
using OpenIddict.Validation.AspNetCore;
using Platform.Shared.Infrastructure.Authorization;
using Platform.Shared.Infrastructure.ExceptionHandling;
using Platform.Shared.Infrastructure.Logging;
using ServiceDefaults;
using Tenant.Application.Extensions;
using Tenant.Domain.Configuration;
using Tenant.Infrastructure.Configuration;
using Tenant.Infrastructure.Extensions;
using Tenant.Infrastructure.Persistence;

LoadEnvFile(Path.Combine(AppContext.BaseDirectory, ".env.development"));

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddTrustedReverseProxy();

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

var projectSettings = new ProjectSettings(builder.Configuration);
var openIddictSettings = new OpenIddictSettings(builder.Configuration);

builder.Services.AddAuthentication();

builder.Services.AddOpenIddict()
    .AddValidation(o =>
    {
        o.SetIssuer(projectSettings.Authority);
        o.AddAudiences($"project:{projectSettings.ProjectId}");

        o.UseIntrospection()
            .SetClientId("tenant-api")
            .SetClientSecret(openIddictSettings.ClientSecret);

        o.UseSystemNetHttp();
        o.UseAspNetCore();
    });

builder.Services.AddAuthorizationBuilder()
    .SetDefaultPolicy(new AuthorizationPolicyBuilder(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .Build());

builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

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
app.UseForwardedHeaders();

app.MapDefaultEndpoints();
app.UseHttpsRedirection();

app.UseCors(app.Services.GetRequiredService<ICorsSettings>().Default);

app.UseExceptionHandler();

app.Use(async (context, next) =>
{
    var requestLogger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Requests");
    var sw = System.Diagnostics.Stopwatch.StartNew();
    await next();
    sw.Stop();
    requestLogger.LogInformation("{Method} {Path} -> {StatusCode} ({ElapsedMs}ms)",
        context.Request.Method, context.Request.Path, context.Response.StatusCode, sw.ElapsedMilliseconds);
});

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
