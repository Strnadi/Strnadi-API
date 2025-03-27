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
using System.Data.Common;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using Shared.Logging;
using LogLevel = Shared.Logging.LogLevel;

namespace Repository;

public abstract class RepositoryBase : IDisposable
{
    protected IConfiguration Configuration { get; }
    
    protected DbConnection Connection { get; }
    
    private string ConnectionString =>
        Configuration["ConnectionStrings:Default"] ??
        throw new NullReferenceException("Failed to upload connection string from .env file");
    
    protected RepositoryBase(IConfiguration configuration)
    {
        Configuration = configuration;
        Connection = new NpgsqlConnection(ConnectionString);
        Connection.Open();
    }

    public void Dispose()
    {
        Connection.Close();
        Connection.Dispose();
    }

    protected async Task ExecuteSafelyAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception e)
        {
            Logger.Log("Failed to execute SQL query: " + e.Message, LogLevel.Error);
        }
    }
    
    protected async Task<T?> ExecuteSafelyAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception e)
        {
            string methodName = new StackTrace().GetFrame(1)?.GetMethod()?.Name ?? "UnknownMethod"; 
            Logger.Log($"Execution of {GetType().Name}.{methodName} failed. ", LogLevel.Error);
            Logger.Log("Failed to execute SQL query: " + e.Message, LogLevel.Error);
            return default;
        }
    }
}