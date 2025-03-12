using Dapper;
using Microsoft.Extensions.Configuration;
using Models.Database;
using Shared.Tools;

namespace Repository;

public class RecordingsRepository : RepositoryBase
{
    public RecordingsRepository(IConfiguration configuration) : base(configuration)
    {
    }

    public async Task<RecordingModel[]?> Get(string? email, bool parts, bool sound)
    {
        return await ExecuteSafelyAsync(async () =>
        {
            RecordingModel[] recordings = (email is not null
                ? await GetByEmailUnsafeAsync(email)
                : await GetAllUnsafeAsync()).ToArray();

            if (parts)
                foreach (var recording in recordings)
                    recording.Parts = await GetPartsAsync(recording.Id, sound);

            return recordings;
        });
    }

    private async Task<IEnumerable<RecordingModel>> GetByEmailUnsafeAsync(string email)
    {
        const string sql = """
                           SELECT * 
                           FROM "Recordings" 
                           WHERE "UserId" = (
                               SELECT "Id" 
                               FROM "Users"
                               WHERE "Email" = @Email
                           );
                           """;
        return await Connection.QueryAsync<RecordingModel>(sql, new { Email = email });
    }

    private async Task<IEnumerable<RecordingModel>> GetAllUnsafeAsync()
    {
        const string sql = """SELECT * FROM "Recordings" """;
        return await Connection.QueryAsync<RecordingModel>(sql);
    }

    public async Task<IEnumerable<RecordingPartModel>?> GetPartsAsync(int recordingId, bool sound)
    {
        return await ExecuteSafelyAsync(async () =>
        {
            var parts = await GetPartsUnsafeAsync(recordingId);

            if (parts is null || !sound)
                return parts;

            var partsArray = parts as RecordingPartModel[] ?? parts.ToArray();
            
            foreach (var part in partsArray)
            {
                string? path = part.FilePath;

                if (path is null)
                    continue;

                var bytes = await File.ReadAllBytesAsync(path);
                part.DataBase64 = Convert.ToBase64String(bytes);
            }

            return partsArray;
        });
    }

    private async Task<IEnumerable<RecordingPartModel>?> GetPartsUnsafeAsync(int recordingId)
    {
        return await ExecuteSafelyAsync(async () =>
        {
            const string sql = """SELECT * FROM "RecordingParts" WHERE "RecordingId" = @RecordingId""";
            return await Connection.QueryAsync<RecordingPartModel>(sql, new { RecordingId = recordingId });
        });
    }
}