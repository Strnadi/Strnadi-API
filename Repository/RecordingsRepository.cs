using Dapper;
using Microsoft.Extensions.Configuration;
using Models.Database;
using Shared.Models.Requests;
using Shared.Tools;

namespace Repository;

public class RecordingsRepository : RepositoryBase
{
    public RecordingsRepository(IConfiguration configuration) : base(configuration)
    {
    }

    public async Task<RecordingModel[]?> GetAsync(string? email, bool parts, bool sound)
    {
        RecordingModel[]? recordings = (email is not null
            ? await GetByEmailAsync(email)
            : await GetAllAsync())?.ToArray();

        if (recordings is null)
            return null;

        if (parts)
            foreach (var recording in recordings)
                recording.Parts = await GetPartsAsync(recording.Id, sound);

        return recordings;
    }

    private async Task<IEnumerable<RecordingModel>?> GetByEmailAsync(string email)
    {
        return await ExecuteSafelyAsync(async () =>
        {
            const string sql = """
                               SELECT * 
                               FROM "Recordings" 
                               WHERE "UserEmail" = @Email
                               """;
            return await Connection.QueryAsync<RecordingModel>(sql, new { Email = email });
        });
    }

    private async Task<IEnumerable<RecordingModel>?> GetAllAsync()
    {
        return await ExecuteSafelyAsync(async () =>
        {
            const string sql = """SELECT * FROM "Recordings" """;
            return await Connection.QueryAsync<RecordingModel>(sql);
        });
    }

    public async Task<RecordingModel?> GetAsync(int id, bool parts, bool sound)
    {
        var recording = await GetAsync(id);
            
        if (recording is null || !parts)
            return recording;
            
        recording.Parts = await GetPartsAsync(id, sound);
            
        return recording;
    }
    
    private async Task<RecordingModel?> GetAsync(int id) =>
        await ExecuteSafelyAsync(async () => await Connection.QueryFirstOrDefaultAsync<RecordingModel>(
            """SELECT * FROM "Recordings" WHERE "Id" = @Id""",
            new
            {
                Id = id
            }));

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

    public async Task<int?> UploadAsync(string email, RecordingUploadModel model)
    {
        return await ExecuteSafelyAsync(async () =>
        {
            const string sql = """
                               INSERT INTO "Recordings"("UserEmail", "CreatedAt", "EstimatedBirdsCount", "Device", "ByApp", "Note", "Name")
                               VALUES (@UserEmail, @CreatedAt, @EstimatedBirdsCount, @Device, @ByApp, @Note, @Name) 
                               RETURNING "Id"
                               """;
            return await Connection.ExecuteScalarAsync<int?>(sql, new
            {
                UserEmail = email,
                model.CreatedAt,
                model.EstimatedBirdsCount,
                model.Device,
                model.ByApp,
                model.Note,
                model.Name
            });
        });
    }

    public async Task<int?> UploadPartAsync(RecordingPartUploadModel model)
    {
        return await ExecuteSafelyAsync(async () =>
        {
            const string sql = """
                               INSERT INTO "RecordingParts"("RecordingId", "Start", "End", "GpsLatitudeStart", "GpsLongitudeStart", "GpsLatitudeEnd", "GpsLongitudeEnd")
                               VALUES (@RecordingId, @Start, @End, @GpsLatitudeStart, @GpsLongitudeStart, @GpsLatitudeEnd, @GpsLongitudeEnd)
                               RETURNING "Id"
                               """;

            return await Connection.ExecuteScalarAsync<int?>(sql, new
            {
                model.RecordingId,
                model.Start,
                model.End,
                model.GpsLatitudeStart,
                model.GpsLongitudeStart,
                model.GpsLatitudeEnd,
                model.GpsLongitudeEnd
            });
        });
    }
}