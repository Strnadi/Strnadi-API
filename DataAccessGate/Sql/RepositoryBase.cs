using System.Data.Common;
using Npgsql;

namespace DataAccessGate.Sql;

internal abstract class RepositoryBase
{
    protected DbConnection _connection;
    
    protected RepositoryBase(string connectionString)
    {
        _connection = new NpgsqlConnection(connectionString);
    }
}