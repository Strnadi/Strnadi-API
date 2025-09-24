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
using Auth.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace Auth;

public static class ServiceCollectionExtensions
{
    public static void AddAuthServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<JwtService>();

        var appleOptions = configuration.GetSection("Auth:Apple").Get<AppleAuthOptions>();
        if (appleOptions is null)
            throw new InvalidOperationException("Missing configuration section 'Auth:Apple'.");
        services.AddSingleton(appleOptions);

        services.AddHttpClient();
        services.AddSwaggerGen();
    }
}