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
using ApiGateway.Services;
using Shared.Communication;
using Shared.Middleware.IpRateLimiter;
using Shared.Middleware.Logging;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

builder.Services.AddHttpClient();
builder.Services.AddLogging();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<DagRecordingsControllerClient>();
builder.Services.AddScoped<DagUsersControllerClient>();
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
builder.Services.AddControllers();

var app = builder.Build();

app.UseMiddleware<IpRateLimitingMiddleware>();

app.UseHttpsRedirection();
app.UseRouting();
app.MapControllers();
app.Run();