using Models.Requests;
using Npgsql;

namespace DataAccessGate.Sql;

internal class UsersRepository : RepositoryBase
{
    private const string users_table_name = "\"Users\"";
    private const string email_column_name = "\"Email\"";
    private const string password_column_name = "\"Password\"";
    
    public UsersRepository(string connectionString) : base(connectionString)
    {
    }

    public bool AuthorizeUser(string email, string password)
    {
        using var command = new NpgsqlCommand($"SELECT * FROM {users_table_name} WHERE {email_column_name} = @Email AND {password_column_name} = @Password");
        
        command.Parameters.AddWithValue("@Email", email);
        command.Parameters.AddWithValue("@Password", password);

        using var reader = command.ExecuteReader();
        return reader.HasRows;
    }

    public bool CreateUser(SignUpRequest request)
    {
        using var command = new NpgsqlCommand($"INSERT INTO ");

        throw new NotImplementedException();
    }
}