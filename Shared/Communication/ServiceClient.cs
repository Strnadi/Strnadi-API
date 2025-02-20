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

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Shared.Logging;

namespace Shared.Communication;

public abstract class ServiceClient
{
    private readonly HttpClient _httpClient;

    private readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    
    protected IConfiguration Configuration { get; }

    protected ServiceClient(IConfiguration configuration, HttpClient httpClient)
    {
        Configuration = configuration;
        _httpClient = httpClient;
    }

    protected async Task<HttpRequestResult?> GetAsync(string route)
    {
        try
        {
            var response = await _httpClient.GetAsync(route);
            return new HttpRequestResult(response);
        }
        catch (Exception ex)
        {
            Logger.Log($"Exception thrown while redirecting request to {route}: {ex.Message}");
            return null;
        }
    }

    protected async Task<HttpRequestResult<TResponse?>?> GetAsync<TResponse>(string route)
    {
        try
        {
            var response = await _httpClient.GetAsync(route);

            if (!response.IsSuccessStatusCode)
                return new HttpRequestResult<TResponse?>(response, default);
            
            var content = await response.Content.ReadAsStringAsync();
            var responseModel = JsonSerializer.Deserialize<TResponse>(content, _jsonSerializerOptions);
            return new HttpRequestResult<TResponse?>(response, responseModel);
        }
        catch (Exception ex)
        {
            Logger.Log($"Exception thrown while redirecting request to {route}: {ex.Message}");
            return null;
        }
    }

    protected async Task<HttpRequestResult?> PostAsync(string route)
    {
        try
        {
            var response = await _httpClient.PostAsync(route, null);
            return new HttpRequestResult(response);
        }
        catch (Exception ex)
        {
            Logger.Log($"Exception thrown while redirecting request to {route}: {ex.Message}");
            return null;
        }
    }

    protected async Task<HttpRequestResult?> PostAsync<TRequest>(string route, TRequest request)
    {
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(route, content);
            return new HttpRequestResult(response);
        }
        catch (Exception ex)
        {
            Logger.Log($"Exception thrown while redirecting request to {route}: {ex.Message}");
            return null;
        }
    }

    protected async Task<HttpRequestResult<TResponse?>?> PostAsync<TRequest, TResponse>(string route, TRequest? request) 
    {
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(route, content);

            return !response.IsSuccessStatusCode
                ? new HttpRequestResult<TResponse?>(response, default)
                : new HttpRequestResult<TResponse?>(response, await SerializeResponseContentAsync<TResponse>(response.Content));
        }
        catch (Exception ex)
        {
            Logger.Log($"Exception thrown while redirecting request to {route}: {ex.Message}");
            return null;
        }
    }

    private async Task<TResponse?> SerializeResponseContentAsync<TResponse>(HttpContent content)
    {
        if (typeof(TResponse) == typeof(string))
            return (TResponse)(object)await content.ReadAsStringAsync();
        
        return content.Headers.ContentType!.MediaType switch
        {
            "application/json" => JsonSerializer.Deserialize<TResponse>(await content.ReadAsStringAsync(),
                _jsonSerializerOptions),
            _ => throw new NotSupportedException($"Unsupported content type: {content.Headers.ContentType.MediaType}")
        };
    }
}