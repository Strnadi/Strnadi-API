using Asp.Versioning;
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
using Tenant.Api.Configuration;
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

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
}).AddMvc()
  .AddApiExplorer(options =>
  {
      options.GroupNameFormat = "'v'VVV";
      options.SubstituteApiVersionInUrl = true;
  });

builder.Services.AddInfrastructure();
builder.Services.AddApplication();

builder.Services.AddDbContext<TenantDbContext>((sp, options) =>
    options.UseNpgsql(sp.GetRequiredService<IDatabaseSettings>().ConnectionString));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<TenantDbContext>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.ConfigureOptions<ConfigureSwaggerOptions>();
builder.Services.AddSwaggerGen(options =>
{
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

// TEMPORARY: old mobile app builds call unprefixed routes (e.g. /recordings). Rewrite anything
// that isn't already /v1/..., /utils/health, /swagger, /list-supported-versions, or the Aspire
// dev-only /health and /alive (ServiceDefaults.MapDefaultEndpoints, Development only) to /v1/...
// so both continue to work during the migration window. Delete this once request logs show ~0
// traffic on the unprefixed paths and the mobile app has shipped its /v1/ update.
app.Use(async (context, next) =>
{
    if (!context.Request.Path.StartsWithSegments("/v1", StringComparison.OrdinalIgnoreCase)
        && !context.Request.Path.StartsWithSegments("/utils/health", StringComparison.OrdinalIgnoreCase)
        && !context.Request.Path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase)
        && !context.Request.Path.StartsWithSegments("/list-supported-versions", StringComparison.OrdinalIgnoreCase)
        && !context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)
        && !context.Request.Path.StartsWithSegments("/alive", StringComparison.OrdinalIgnoreCase))
    {
        context.Request.Path = "/v1" + context.Request.Path;
    }
    await next();
});

// Explicit on purpose: MapDefaultEndpoints() above is itself a Map* call, so without UseRouting()
// called somewhere, ASP.NET Core implicitly inserts endpoint matching right there - before the
// rewrite middleware above ever runs. That silently matched (or 404'd) requests against the
// original unprefixed path and made the rewrite a no-op for routing purposes. Calling UseRouting()
// here pins matching to happen after the rewrite instead.
app.UseRouting();

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
// Same health checks, reachable under the versioned prefix too - mapping the same checks at two
// paths is fine, ASP.NET Core doesn't restrict a health check registration to a single route.
app.MapHealthChecks("/v1/utils/health");

// Public capability-discovery manifest (.well-known convention, same idea as an OIDC discovery
// document) - lets a consumer (Administration.Api today, the mobile app later) ask "what do you
// support" instead of assuming full parity with our current HEAD. Our own reference deployment
// always supports everything it has; a third-party Tenant.Api implementation is expected to
// return only the subset it actually implements.
app.MapGet("/v1/.well-known/capabilities", () => Results.Ok(new
{
    apiVersion = "1.0",
    features = new[]
    {
        "recordings", "recording-parts", "recording-photos", "filtered-recordings",
        "detected-dialects", "achievements", "articles", "article-categories", "devices",
        "map", "map-clusters", "notifications"
    }
})).AllowAnonymous();

// Hardcoded for now - lets a consumer (Administration.Api, the mobile app) check which of its
// own versions this deployment still supports without hitting a specific feature endpoint.
// Update the list by hand as versions are released/retired.
app.MapGet("/list-supported-versions", () => Results.Ok(new[] { "2.1.12" })).AllowAnonymous();

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
