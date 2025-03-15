using Dapper;
using Microsoft.Extensions.Configuration;
using Shared.Models.Requests.Photos;

namespace Repository;

public class PhotosRepository : RepositoryBase
{
    public PhotosRepository(IConfiguration configuration) : base(configuration)
    {
    }

    public async Task<bool> UploadAsync(UploadRecordingPhotoRequest request) => await ExecuteSafelyAsync(async () =>
    {
       const string sql = "INSERT INTO photos(recording_id) VALUES (@RecordingId);";

       return await Connection.ExecuteAsync(sql, new
       {
           RecordingId = request.RecordingId
       }) != 1;
    });
}