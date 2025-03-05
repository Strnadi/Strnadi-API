/*
 * Copyright (C) 2024 Stanislav Motsnyi
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
using Microsoft.Extensions.Configuration;
using Models.Database;
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
    
    public async Task<HttpRequestResult?> VerifyUser(string email)
    {
        string url = GetVerifyUserUrl(email);

        return await PostAsync(url);
    }

    public async Task<HttpRequestResult<UserModel?>?> GetUser(string email)
    {
        string url = GetUserUrl(email);

        return await GetAsync<UserModel>(url);
    }

    public async Task<HttpRequestResult<string?>?> GetUserFcmToken(string email)
    {
        string url = GetUserFcmTokenUrl(email);
        
        return await GetAsync<string>(url);
    } 
    
    public async Task<HttpRequestResult<bool?>?> IsAdminAsync(string? email)
    {
        string url = GetIsAdminUrl(email);
        
        return await GetAsync<bool?>(url);
    }

    private string GetUserUrl(string email) =>
         $"http://{_dagCntName}:{_dagCntPort}/users/{email}";
    
    private string GetAuthorizeUserUrl() =>
        $"http://{_dagCntName}:{_dagCntPort}/users/authorize-user";
    
    private string GetSignUpUrl() =>
        $"http://{_dagCntName}:{_dagCntPort}/users/sign-up";

    private string GetVerifyUserUrl(string email) =>
        $"http://{_dagCntName}:{_dagCntPort}/users/verify?email={email}";

    private string GetUserFcmTokenUrl(string email) =>
        $"http://{_dagCntName}:{_dagCntPort}/users/{email}/fcm-token";
    
    private string GetIsAdminUrl(string? email) =>
        $"http://{_dagCntName}:{_dagCntPort}/users/{email}/is-admin";

}