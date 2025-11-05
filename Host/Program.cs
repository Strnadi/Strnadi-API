/*
 * Copyright (C) 2024 Stanislav Motsnyi
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */

using System.Reflection;
using System.Text;
using Achievements;
using Articles;
using Auth;
using Devices;
using Dictionary;
using Email;
using Microsoft.OpenApi.Readers;
using Microsoft.OpenApi.Writers;
using Photos;
using Recordings;
using Repository;
using Shared.Extensions;
using Users;
using Utils;

namespace Host;

class Program
{
    static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var configuration = builder.Configuration;

        ConfigureServices(builder, configuration);
        
        var app = builder.Build();

        ConfigureApp(app, configuration);
        
        app.Run();
    }

    static void ConfigureServices(WebApplicationBuilder builder, ConfigurationManager configuration)
    {
        builder.Services.AddMemoryCache();
        builder.Services.AddLogging();
        builder.Services.AddHttpClient();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddTools(configuration);
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(UsersController).Assembly)
            .AddApplicationPart(typeof(AuthController).Assembly)
            .AddApplicationPart(typeof(RecordingsController).Assembly)
            .AddApplicationPart(typeof(DevicesController).Assembly)
            .AddApplicationPart(typeof(UtilsController).Assembly)
            .AddApplicationPart(typeof(PhotosController).Assembly)
            .AddApplicationPart(typeof(ArticlesController).Assembly)
            .AddApplicationPart(typeof(DictionaryController).Assembly)
            .AddApplicationPart(typeof(AchievementsController).Assembly);
        builder.Services.AddRepositories();
        builder.Services.AddEmailServices();
        builder.Services.AddAuthServices();
        builder.Services.AddCors(corsOptions =>
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
        var openApiDocument = LoadEmbeddedOpenApiDocument();

        app.UseCors(configuration["CORS:Default"]);
        app.UseHttpsRedirection();
        app.UseRouting();
        app.MapControllers();
        app.MapGet("/swagger/StrnadiAPI-openapi.yaml", () => Results.Text(openApiDocument.Yaml, "application/yaml"));
        app.MapGet("/swagger/v1/swagger.json", () => Results.Text(openApiDocument.Json, "application/json"));
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Strnadi API");
            options.RoutePrefix = string.Empty;
            options.DocumentTitle = "Strnadi API - Swagger";
        });
    }

    static OpenApiDocumentContent LoadEmbeddedOpenApiDocument()
    {
        const string resourceName = "Host.StrnadiAPI-openapi.yaml";
        var assembly = Assembly.GetExecutingAssembly();

        using Stream? stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            throw new InvalidOperationException($"Embedded OpenAPI document '{resourceName}' was not found.");
        }

        using var reader = new StreamReader(stream);
        var yaml = reader.ReadToEnd();
        var json = ConvertYamlToJson(yaml);

        return new OpenApiDocumentContent(yaml, json);
    }

    static string ConvertYamlToJson(string yaml)
    {
        var openApiDocument = new OpenApiStringReader().Read(yaml, out var diagnostic);
        if (diagnostic.Errors.Count > 0)
        {
            var errorMessage = string.Join(Environment.NewLine, diagnostic.Errors.Select(error => error.Message));
            throw new InvalidOperationException($"The embedded OpenAPI document contains errors:{Environment.NewLine}{errorMessage}");
        }

        using var stream = new MemoryStream();
        using (var textWriter = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            var jsonWriter = new OpenApiJsonWriter(textWriter);
            openApiDocument.SerializeAsV3(jsonWriter);
            textWriter.Flush();
        }

        stream.Position = 0;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private sealed record OpenApiDocumentContent(string Yaml, string Json);
}
