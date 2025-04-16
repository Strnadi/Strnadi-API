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

using Auth;
using Devices;
using Email;
using Photos;
using Recordings;
using Repository;
using Shared.Logging;
using Users;
using Utils;

namespace Host;

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
        services.AddControllers()
            .AddApplicationPart(typeof(UsersController).Assembly)
            .AddApplicationPart(typeof(AuthController).Assembly)
            .AddApplicationPart(typeof(RecordingsController).Assembly)
            .AddApplicationPart(typeof(DevicesController).Assembly)
            .AddApplicationPart(typeof(UtilsController).Assembly)
            .AddApplicationPart(typeof(PhotosController).Assembly);
        services.AddRepositories();
        services.AddEmailServices();
        services.AddAuthServices();
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
        services.AddSwaggerGen();
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
            options.DocumentTitle = "Strnadi API - Swagger";
        });
    }
}