using Dapper;
using Microsoft.Extensions.Configuration;
using Shared.Models.Requests.Photos;
using Shared.Tools;

namespace Repository;

public class PhotosRepository : RepositoryBase
{
    public PhotosRepository(IConfiguration configuration) : base(configuration)
    {
    }

    public async Task<bool> UploadAsync(UploadRecordingPhotoRequest request) => await ExecuteSafelyAsync(async () =>
    {
        int id = await InsertAsync(request);

        string path = await SaveRecordingPhotoAsync(request.RecordingId, id, request.PhotosBase64, request.Format);

        return await UpdatePhotoFile(id, path);
    });

    private async Task<bool> UpdatePhotoFile(int id, string path)
    {
        const string sql = "UPDATE photos SET file_path=@FilePath WHERE id=@Id";

        return await Connection.ExecuteAsync(sql, new { Id = id, FilePath = path }) != 0;
    }

    private async Task<int> InsertAsync(UploadRecordingPhotoRequest request) => await ExecuteSafelyAsync(async () =>
    {
        const string sql = "INSERT INTO photos(recording_id) VALUES (@RecordingId) RETURNING id;";

        return await Connection.ExecuteScalarAsync<int>(sql, new
        {
            request.RecordingId
        });
    });
    
    private async Task<string> SaveRecordingPhotoAsync(int recordingId, int photoId, string base64, string format)
    {
        var fs = new FileSystemHelper();
        return await fs.SaveRecordingPhotoFileAsync(recordingId, photoId, base64, format);
    }
}