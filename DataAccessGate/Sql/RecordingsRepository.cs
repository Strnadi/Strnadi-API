using Models.Requests;

namespace DataAccessGate.Sql;

internal class RecordingsRepository : RepositoryBase
{
    public RecordingsRepository(string connectionString) : base(connectionString)
    {
    }
    
    public void AddRecording(RecordingUploadReq request)
    {
        using var command = _connection.CreateCommand();

        command.CommandText =
            "INSERT INTO \"Recordings\"(\"UserId\", \"CreatedAt\", \"EstimatedBirdsCount\", \"State\", \"Device\", \"ByApp\", \"Note\")" +
            "VALUES (@UserId, @CreatedAt, @EstimatedBirdsCount, @State, @Device, @ByApp, @Note)";
        
        command.Parameters.
    }
}