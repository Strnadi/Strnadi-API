using Microsoft.Extensions.Configuration;
using Models.Requests;

namespace Shared.Communication;

public class DagUsersControllerClient : ServiceClient
{
    private string _dagCntName => Configuration["MSAddresses:DagName"] ?? throw new NullReferenceException("Failed to load microservice name");
    
    private string _dagCntPort => Configuration["MSAddresses:DagPort"] ?? throw new NullReferenceException("Failed to load microservice port");

    
    public DagUsersControllerClient(IConfiguration configuration, HttpClient httpClient) : base(configuration, httpClient)
    {
    }
    
    public async Task<HttpRequestResult?> AuthorizeUserAsync(LoginRequest request)
    {
        string url = GetAuthorizeUserUrl();
        
        return await PostAsync(url, request);
    }
    
    public async Task<HttpRequestResult?> SignUpAsync(SignUpRequest request)
    {
        string url = GetSignUpUrl();
        
        return await PostAsync(url, request);
    }
    
    public async Task<HttpRequestResult> VerifyUser(string email)
    {
        string url = GetVerifyUserUrl(email);

        return await PostAsync(url);
    }

    private string GetAuthorizeUserUrl() =>
        $"http://{_dagCntName}:{_dagCntPort}/users/authorize-user";
    
    private string GetSignUpUrl() =>
        $"http://{_dagCntName}:{_dagCntPort}/users/sign-up";

    private string GetVerifyUserUrl(string email) =>
        $"http://{_dagCntName}:{_dagCntPort}/users/verify?email={email}";
}