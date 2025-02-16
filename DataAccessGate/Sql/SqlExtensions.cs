using System.Data.Common;

namespace DataAccessGate.Sql;

internal static class SqlExtensions
{
    internal static T? GetValue<T>(this DbDataReader reader, string columnName) 
    {
        object value = reader[columnName];
        
        return value != DBNull.Value ? (T)value : default;
    }
}