using System.Data.Common;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Shared.Logging;

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
    
    protected async Task<T?> ExecuteSafelyAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception e)
        {
            Logger.Log("Failed to execute SQL query: " + e.Message, LogLevel.Error);
            return default;
        }
    }
}