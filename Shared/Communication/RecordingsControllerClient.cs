using Microsoft.Extensions.Configuration;
using Models.Database;

namespace Shared.Communication;

public class RecordingsControllerClient : ServiceClient
{
    private string _dagCntName => Configuration["MSAddresses:DagName"] ?? throw new NullReferenceException("Failed to load microservice name");
    private string _dagCntPort => Configuration["MSAddresses:DagPort"] ?? throw new NullReferenceException("Failed to load microservice port");
    
    public RecordingsControllerClient(IConfiguration configuration,
        HttpClient httpClient) : base(configuration,
        httpClient)
    {
    }

    public async Task<HttpRequestResult<FilteredSubrecordingModel[]?>?> GetFilteredSubrecordingsAsync()
    {
        string url = GetFilteredSubrecordingsUrl();
        return await GetAsync<FilteredSubrecordingModel[]>(url);
    }

    private string GetFilteredSubrecordingsUrl() =>
        $"http://{_dagCntName}:{_dagCntPort}";
}