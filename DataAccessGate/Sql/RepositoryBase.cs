using System.Data.Common;
using Npgsql;
using Shared.Logging;


namespace DataAccessGate.Sql;

internal abstract class RepositoryBase : IDisposable
{
    protected DbConnection _connection;
    protected string _connectionString;
    
    protected RepositoryBase(string connectionString)
    {
        _connectionString = connectionString;
        Logger.Log(connectionString);
        _connection = new NpgsqlConnection(connectionString);
        _connection.Open();
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }
}