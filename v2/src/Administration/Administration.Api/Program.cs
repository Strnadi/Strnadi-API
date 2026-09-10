using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Administration.Api.Logging;
using Administration.Api.Resources;
using Administration.Domain.Configuration;
using Administration.Domain.Entities;
using Administration.Domain.Services;
using Administration.Infrastructure.Configuration;
using Administration.Infrastructure.Email;
using Administration.Infrastructure.Identity;
using Administration.Infrastructure.Persistence;
using Administration.Infrastructure.Security;
using Administration.Infrastructure.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Console;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using Scalar.AspNetCore;
using ServiceDefaults;

const string clientId = "strnadi-app";

// Czech is the primary/default audience (strnadi.cz); English is the only other supported UI
// language for now. Keep in sync with Resources/SharedResource*.resx culture suffixes.
string[] supportedCultures = ["cs", "en"];

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddTrustedReverseProxy();
if (builder.Configuration["DataProtection:KeysPath"] is { Length: > 0 } keysPath)
    builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keysPath));
builder.Services.AddHealthChecks().AddDbContextCheck<AdminDbContext>();

builder.Services.AddDbContext<AdminDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default"))
        .UseSnakeCaseNamingConvention());

builder.Services.AddSingleton<IEncryptionSettings, EncryptionSettings>();
builder.Services.AddSingleton<IEncryptionService, AesEncryptionService>();

builder.Services.AddSingleton<IGoogleAuthSettings, GoogleAuthSettings>();
builder.Services.AddSingleton<IAppleAuthSettings, AppleAuthSettings>();
builder.Services.AddSingleton<IAuthSettings, AuthSettings>();

builder.Services.AddSingleton<ISmtpSettings, SmtpSettings>();
builder.Services.AddScoped<IEmailSender<User>, SmtpEmailSender>();

builder.Services.AddSingleton<IFileStorageSettings, LocalStorageSettings>();
builder.Services.AddSingleton<IFileStorage, LocalStorage>();

builder.Services.AddSingleton<IScalarSettings, ScalarSettings>();

builder.Services.AddIdentityCore<User>(o => o.User.RequireUniqueEmail = true)
    .AddRoles<Role>()
    .AddSignInManager()
    .AddDefaultTokenProviders()
    .AddEntityFrameworkStores<AdminDbContext>();

builder.Services.RemoveAll<IPasswordHasher<User>>();
builder.Services.AddScoped<IPasswordHasher<User>, BCryptPasswordHasher>();

builder.Services.RemoveAll<IUserValidator<User>>();
builder.Services.AddScoped<IUserValidator<User>, CustomUserValidator>();

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
        {
            o.AddDevelopmentEncryptionCertificate()
                .AddDevelopmentSigningCertificate();
        }
        else
        {
            o.AddSigningCertificate(LoadCertificate(builder.Configuration, "OAuth:Signing"))
                .AddEncryptionCertificate(LoadCertificate(builder.Configuration, "OAuth:Encryption"));
        }
    })
    .AddValidation(o =>
    {
        o.UseLocalServer();
        o.UseAspNetCore();
    });

builder.Services.AddControllers();
// No ResourcesPath here: SharedResource already lives in the Resources/ folder/namespace, so its
// resx base name is "Administration.Api.Resources.SharedResource" already. Setting ResourcesPath
// would prepend "Resources." a second time and the localizer would never find a match.
builder.Services.AddLocalization();
builder.Services.AddRazorPages()
    .AddDataAnnotationsLocalization(o =>
        o.DataAnnotationLocalizerProvider = (_, factory) => factory.Create(typeof(SharedResource)));
builder.Services.AddOpenApi();

builder.Services.AddSingleton<ConsoleFormatter, CompactConsoleFormatter>();
builder.Logging.AddConsole(options => options.FormatterName = "compact");

var app = builder.Build();
app.UseForwardedHeaders();

using (var scope = app.Services.CreateScope())
    await SyncOpenIddictClientAsync(scope.ServiceProvider);

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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();
app.MapDefaultEndpoints();
app.MapHealthChecks("/utils/health");

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

static async Task SyncOpenIddictClientAsync(IServiceProvider services)
{
    var applicationManager = services.GetRequiredService<IOpenIddictApplicationManager>();
    var db = services.GetRequiredService<AdminDbContext>();

    var domains = await db.Projects
        .Where(p => p.Domain != null && p.Domain != "")
        .Select(p => p.Domain)
        .ToListAsync();

    var descriptor = new OpenIddictApplicationDescriptor
    {
        ClientId = clientId,
        ClientType = OpenIddictConstants.ClientTypes.Public,
        RedirectUris = { new Uri("com.delta.strnadi://auth/callback") },
        Permissions =
        {
            OpenIddictConstants.Permissions.Endpoints.Authorization,
            OpenIddictConstants.Permissions.Endpoints.Token,
            OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
            OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
            OpenIddictConstants.Permissions.Prefixes.GrantType + OpenIddictConstants.GrantTypes.TokenExchange,
            OpenIddictConstants.Permissions.ResponseTypes.Code
        }
    };

    foreach (var domain in domains)
    {
        descriptor.RedirectUris.Add(new Uri($"{domain.TrimEnd('/')}/ucet/prihlaseni"));
        descriptor.RedirectUris.Add(new Uri($"{domain.TrimEnd('/')}/ucet/registrace"));
    }

    var existing = await applicationManager.FindByClientIdAsync(clientId);
    if (existing is null)
        await applicationManager.CreateAsync(descriptor);
    else
        await applicationManager.UpdateAsync(existing, descriptor);
}
