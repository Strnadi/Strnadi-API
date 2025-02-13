using Models.Requests;
using Npgsql;

namespace DataAccessGate.Sql;

internal class UsersRepository : RepositoryBase
{
    private const string users_table_name = "\"Users\"";
    
    private const string email_column_name = "\"Email\"";
    private const string nickname_column_name = "\"Nickname\"";
    private const string password_column_name = "\"Password\"";
    private const string first_name_column_name = "\"FirstName\"";
    private const string last_name_column_name = "\"LastName\"";
    
    public UsersRepository(string connectionString) : base(connectionString)
    {
    }

    public bool AuthorizeUser(string email, string password)
    {
        using var command = (NpgsqlCommand)_connection.CreateCommand();

        command.CommandText =
            $"SELECT * FROM {users_table_name} WHERE {email_column_name} = @Email AND {password_column_name} = @Password";
        
        command.Parameters.AddWithValue("@Email", email);
        command.Parameters.AddWithValue("@Password", password);

        using var reader = command.ExecuteReader();
        return reader.HasRows;
    }

    public bool CreateUser(SignUpRequest request)
    {
        using var command = (NpgsqlCommand)_connection.CreateCommand();
        
        command.CommandText = $"INSERT INTO {users_table_name} ({nickname_column_name}, " +
                              $"{email_column_name}, {password_column_name}, {first_name_column_name}, {last_name_column_name}) VALUES" +
                              $"VALUES (@Nickname, @Email, @Password, @FirstName, @LastName)";
        
#pragma warning disable CS8604 // Nickname column is nullable
        command.Parameters.AddWithValue("@Nickname", request.Nickname);
#pragma warning restore CS8604 //
        command.Parameters.AddWithValue("@Email", request.Email);
        command.Parameters.AddWithValue("@Password", request.Password);
        command.Parameters.AddWithValue("@FirstName", request.FirstName);
        command.Parameters.AddWithValue("@LastName", request.LastName);
        
        return command.ExecuteNonQuery() is 1;
    }
}