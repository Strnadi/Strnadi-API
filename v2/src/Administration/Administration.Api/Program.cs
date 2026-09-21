using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Administration.Api.Resources;
using Administration.Api.Services;
using Administration.Domain.Configuration;
using Administration.Domain.Entities;
using Administration.Domain.Persistence.Repositories;
using Administration.Domain.Services;
using Administration.Infrastructure.Configuration;
using Administration.Infrastructure.Email;
using Administration.Infrastructure.Identity;
using Administration.Infrastructure.Persistence;
using Administration.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Console;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using Platform.Shared.Common.Extensions;
using Platform.Shared.Infrastructure.Configuration;
using Platform.Shared.Infrastructure.ExceptionHandling;
using Platform.Shared.Infrastructure.Logging;
using Platform.Shared.Infrastructure.Security;
using Platform.Shared.Infrastructure.Storage;
using Platform.Shared.Kernel.Configuration;
using Platform.Shared.Kernel.Exceptions;
using Platform.Shared.Kernel.Services;
using Scalar.AspNetCore;
using ServiceDefaults;

const string tenantApiClientId = "tenant-api";

string[] supportedCultures = ["cs", "en", "de"];

LoadEnvFile(Path.Combine(AppContext.BaseDirectory, "preprod.env"));

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddTrustedReverseProxy();
if (!builder.Environment.IsDevelopment() && builder.Configuration["DataProtection:KeysPath"] is { Length: > 0 } keysPath)
    builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keysPath));
builder.Services.AddHealthChecks().AddDbContextCheck<AdminDbContext>();

builder.Services.AddDbContext<AdminDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default"))
        .UseSnakeCaseNamingConvention());

builder.Services.AddScoped<IProjectAccessSync, ProjectAccessSync>();

builder.Services.AddSingleton<IEncryptionSettings, EncryptionSettings>();
builder.Services.AddSingleton<IEncryptionService, AesEncryptionService>();

builder.Services.AddSingleton<IGoogleAuthSettings, GoogleAuthSettings>();
builder.Services.AddSingleton<IAppleAuthSettings, AppleAuthSettings>();
builder.Services.AddSingleton<IAuthSettings, AuthSettings>();

builder.Services.AddSingleton<IOpenIddictSettings, OpenIddictSettings>();
builder.Services.AddSingleton<ITenantApiSettings, TenantApiSettings>();

// Used to fetch a project's declared capability manifest / OpenAPI spec, and our own reference
// Tenant.Api's - a project's ApiDomain is untrusted third-party input, hence the short timeout.
builder.Services.AddHttpClient("TenantApiClient", c => c.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddScoped<ITenantConformanceChecker, TenantConformanceChecker>();

builder.Services.AddSingleton<ISmtpSettings, SmtpSettings>();
builder.Services.AddScoped<IEmailSender<User>, SmtpEmailSender>();
builder.Services.AddScoped<IDocumentEmailSender, SmtpEmailSender>();

builder.Services.AddFileStorage();

builder.Services.AddSingleton<IScalarSettings, ScalarSettings>();

builder.Services.AddScoped<IUserPermissionsRepository, UserPermissionsRepository>();

builder.Services.AddCors();

builder.Services.AddIdentityCore<User>(o => o.User.RequireUniqueEmail = true)
    .AddRoles<Role>()
    .AddSignInManager()
    .AddDefaultTokenProviders()
    .AddEntityFrameworkStores<AdminDbContext>();

builder.Services.RemoveAll<IPasswordHasher<User>>();
builder.Services.AddScoped<IPasswordHasher<User>, BCryptPasswordHasher>();

builder.Services.RemoveAll<IUserValidator<User>>();
builder.Services.AddScoped<IUserValidator<User>, CustomUserValidator>();

builder.Services.RemoveAll<IUserClaimsPrincipalFactory<User>>();
builder.Services.AddScoped<IUserClaimsPrincipalFactory<User>, EmailClaimsPrincipalFactory>();

var googleAuthSettings = new GoogleAuthSettings(builder.Configuration);
var appleAuthSettings = new AppleAuthSettings(builder.Configuration);

builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddCookie(IdentityConstants.ApplicationScheme, options =>
    {
        options.LoginPath = "/account/login";
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    })
    .AddCookie(IdentityConstants.ExternalScheme, options =>
    {
        options.Cookie.Name = IdentityConstants.ExternalScheme;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddGoogle(options =>
    {
        options.ClientId = googleAuthSettings.ClientId;
        options.ClientSecret = googleAuthSettings.ClientSecret;
        options.SignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddApple(options =>
    {
        options.GenerateClientSecret = true;
        options.ClientId = appleAuthSettings.ClientId;
        options.TeamId = appleAuthSettings.TeamId;
        options.KeyId = appleAuthSettings.KeyId;
        options.PrivateKey = (keyId, ct) => Task.FromResult(appleAuthSettings.P8PrivateKey.AsMemory());
        options.SignInScheme = IdentityConstants.ExternalScheme;
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AccountMutation", policy => policy
        .AddAuthenticationSchemes(IdentityConstants.ApplicationScheme, OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser());

builder.Services.AddOpenIddict()
    .AddCore(o => o.UseEntityFrameworkCore().UseDbContext<AdminDbContext>())
    .AddServer(o =>
    {
        o.SetAuthorizationEndpointUris("/connect/authorize")
            .SetTokenEndpointUris("/connect/token")
            .SetUserInfoEndpointUris("/connect/user-info")
            .SetEndSessionEndpointUris("/connect/logout")
            .SetIntrospectionEndpointUris("/connect/introspect")
            .AllowAuthorizationCodeFlow()
            .RequireProofKeyForCodeExchange()
            .AllowRefreshTokenFlow()
            .AllowTokenExchangeFlow()
            .DisableAccessTokenEncryption()
            .UseAspNetCore()
            .EnableAuthorizationEndpointPassthrough()
            .EnableTokenEndpointPassthrough()
            .EnableUserInfoEndpointPassthrough()
            .EnableEndSessionEndpointPassthrough();

        if (builder.Environment.IsDevelopment())
            o.AddDevelopmentEncryptionCertificate()
                .AddDevelopmentSigningCertificate();
        else
            o.AddSigningCertificate(LoadCertificate(builder.Configuration, "OAuth:Signing"))
                .AddEncryptionCertificate(LoadCertificate(builder.Configuration, "OAuth:Encryption"));
    })
    .AddValidation(o =>
    {
        o.UseLocalServer();
        o.UseAspNetCore();
    });

builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddControllers();
builder.Services.AddLocalization();
builder.Services.AddRazorPages()
    .AddDataAnnotationsLocalization(o =>
        o.DataAnnotationLocalizerProvider = (_, factory) => factory.Create(typeof(SharedResource)));
builder.Services.AddOpenApi();

builder.Services.AddSingleton<ConsoleFormatter, CompactConsoleFormatter>();
builder.Logging.AddConsole(options => options.FormatterName = "compact");

var app = builder.Build();
app.UseForwardedHeaders();

// Browser navigations (Accept: text/html) get redirected to a friendly Pages/Error.cshtml instead
// of the JSON ProblemDetails that DomainExceptionHandler writes for API clients - JSON callers
// keep the existing behavior below.
bool WantsHtml(HttpContext context) =>
    context.Request.Headers.Accept.Any(a => a is not null && a.Contains("text/html"));

app.UseWhen(WantsHtml, branch =>
{
    branch.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(context =>
        {
            var exception = context.Features.Get<IExceptionHandlerPathFeature>()?.Error;
            var statusCode = exception switch
            {
                NotFoundException => StatusCodes.Status404NotFound,
                ConflictException => StatusCodes.Status409Conflict,
                ForbiddenException => StatusCodes.Status403Forbidden,
                UnauthorizedException => StatusCodes.Status401Unauthorized,
                ValidationException => StatusCodes.Status400BadRequest,
                _ => StatusCodes.Status500InternalServerError
            };

            context.RequestServices.GetRequiredService<ILogger<Program>>().LogError(
                exception, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);

            context.Response.Redirect($"/error/{statusCode}");
            return Task.CompletedTask;
        });
    });

    // Also catches plain error-status results (e.g. a controller returning NotFound()) that
    // didn't throw, preserving the original URL instead of redirecting.
    branch.UseStatusCodePagesWithReExecute("/error/{0}");
});

app.UseWhen(context => !WantsHtml(context), branch => branch.UseExceptionHandler());

app.UseRouting();
app.UseCors(ProjectAccessSync.CorsPolicyName);

using (var scope = app.Services.CreateScope())
{
    if (app.Environment.IsDevelopment())
        await scope.ServiceProvider.GetRequiredService<AdminDbContext>().Database.MigrateAsync();

    await scope.ServiceProvider.GetRequiredService<IProjectAccessSync>().SyncAsync();

    var openIddictSettings = scope.ServiceProvider.GetRequiredService<IOpenIddictSettings>();
    await SyncTenantApiClientAsync(scope.ServiceProvider, openIddictSettings.TenantClientSecret);
}

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture(supportedCultures[0])
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures));

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
app.MapRazorPages();
app.MapDefaultEndpoints();
app.MapHealthChecks("/utils/health");

// Hardcoded for now - the mobile app (and eventually Tenant.Api deployments) can check this to
// know which of their own versions this backend still supports, without hitting a specific
// feature endpoint. Update the list by hand as versions are released/retired.
app.MapGet("/list-supported-versions", () => Results.Ok(new[] { "2.1.12" })).AllowAnonymous();

app.MapGet("/", (HttpContext context) => Results.LocalRedirect(
    context.User.Identity?.IsAuthenticated == true ? "/dashboard" : "/Account/Login?returnUrl=/dashboard"));

app.MapGet("/culture/set", (string culture, string? returnUrl, HttpContext context) =>
{
    if (!supportedCultures.Contains(culture))
        return Results.BadRequest();

    context.Response.Cookies.Append(
        CookieRequestCultureProvider.DefaultCookieName,
        CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
        new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true });

    return Results.LocalRedirect(string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl);
});

if (!app.Environment.IsDevelopment())
{
    var scalarSettings = app.Services.GetRequiredService<IScalarSettings>();
    var scalarUsername = scalarSettings.Username;
    var scalarPassword = scalarSettings.Password;
    if (string.IsNullOrEmpty(scalarUsername) || string.IsNullOrEmpty(scalarPassword))
    {
        app.Logger.LogWarning(
            "Scalar:Username/Scalar:Password are not configured - /scalar and /openapi will reject every request until both are set");
    }

    app.Use(async (context, next) =>
    {
        if (!context.Request.Path.StartsWithSegments("/scalar") && !context.Request.Path.StartsWithSegments("/openapi"))
        {
            await next();
            return;
        }

        if (string.IsNullOrEmpty(scalarUsername) || string.IsNullOrEmpty(scalarPassword)
                                                 || !HasValidScalarCredentials(context.Request, scalarUsername, scalarPassword))
        {
            context.Response.Headers.WWWAuthenticate = "Basic realm=\"Scalar\"";
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await next();
    });
}

app.MapOpenApi();
app.MapScalarApiReference(o => o.WithOperationTitleSource(OperationTitleSource.Path));

app.Run();

static bool HasValidScalarCredentials(HttpRequest request, string expectedUsername, string expectedPassword)
{
    var header = request.Headers.Authorization.ToString();
    if (!header.StartsWith("Basic ", StringComparison.Ordinal))
        return false;

    string decoded;
    try
    {
        decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header["Basic ".Length..]));
    }
    catch (FormatException)
    {
        return false;
    }

    var separatorIndex = decoded.IndexOf(':');
    if (separatorIndex < 0)
        return false;

    var username = decoded[..separatorIndex];
    var password = decoded[(separatorIndex + 1)..];
    return username == expectedUsername && password == expectedPassword;
}

static X509Certificate2 LoadCertificate(IConfiguration configuration, string configSection) =>
    X509CertificateLoader.LoadPkcs12(
        Convert.FromBase64String(configuration[$"{configSection}:Pfx"]!),
        configuration[$"{configSection}:Password"]);

static async Task SyncTenantApiClientAsync(IServiceProvider services, string clientSecret)
{
    var applicationManager = services.GetRequiredService<IOpenIddictApplicationManager>();

    var descriptor = new OpenIddictApplicationDescriptor
    {
        ClientId = tenantApiClientId,
        ClientSecret = clientSecret,
        ClientType = OpenIddictConstants.ClientTypes.Confidential,
        Permissions =
        {
            OpenIddictConstants.Permissions.Endpoints.Introspection
        }
    };

    var existing = await applicationManager.FindByClientIdAsync(tenantApiClientId);
    if (existing is null)
        await applicationManager.CreateAsync(descriptor);
    else
        await applicationManager.UpdateAsync(existing, descriptor);
}

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