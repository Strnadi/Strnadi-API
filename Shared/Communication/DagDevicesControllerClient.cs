using Microsoft.Extensions.Configuration;
using Models.Requests;

namespace Shared.Communication;

public class DagDevicesControllerClient : ServiceClient
{
    private string _dagCntName => Configuration["MSAddresses:DagName"] ?? throw new NullReferenceException("Failed to load microservice name");
    private string _dagCntPort => Configuration["MSAddresses:DagPort"] ?? throw new NullReferenceException("Failed to load microservice port");
    
    public DagDevicesControllerClient(IConfiguration configuration, HttpClient httpClient) : base(configuration, httpClient)
    {
    }
    
    public async Task<HttpRequestResult?> Device(string email, DeviceRequest model)
    {
        string url = GetDeviceUrl(email);
        
        return await PostAsync(url, model);
    }
    
    private string GetDeviceUrl(string email) =>
        $"http://{_dagCntName}:{_dagCntPort}/devices/{email}/device";
}