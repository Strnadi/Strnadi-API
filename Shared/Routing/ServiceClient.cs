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
using Shared.Logging;

namespace Shared.Routing;

public abstract class ServiceClient
{
    protected HttpClient HttpClient { get; private set; }

    protected ServiceClient(HttpClient httpClient)
    {
        HttpClient = httpClient;
    }

    protected async Task<HttpResponseMessage?> GetAsync(string route)
    {
        try
        {
            var response = await HttpClient.GetAsync(route);
            return response;
        }
        catch (Exception ex)
        {
            Logger.Log($"Exception thrown while redirecting request to {route}: {ex.Message}");
            return null;
        }
    }

    protected async Task<(TResponse? Response, HttpResponseMessage Message)> GetAsync<TResponse>(string route)
    {
        var response = await HttpClient.GetAsync(route);
        if (!response.IsSuccessStatusCode)
            return default;

        try
        {
            var content = await response.Content.ReadAsStringAsync();
            return (JsonSerializer.Deserialize<TResponse>(content), response);
        }
        catch (Exception ex)
        {
            Logger.Log($"Exception thrown while redirecting request to {route}: {ex.Message}");
            return default;
        }
    }

    protected async Task<HttpResponseMessage?> PostAsync<TRequest>(string route, TRequest request)
    {
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await HttpClient.PostAsync(route, content);
            return response;
        }
        catch (Exception ex)
        {
            Logger.Log($"Exception thrown while redirecting request to {route}: {ex.Message}");
            return null;
        }
    }

    protected async Task<(TResponse? Response, HttpResponseMessage Message)> PostAsync<TRequest, TResponse>(string route, TRequest? request)
    {
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await HttpClient.PostAsync(route, content);

            if (!response.IsSuccessStatusCode)
                return default;

            var responseContent = await response.Content.ReadAsStringAsync();
            return (JsonSerializer.Deserialize<TResponse>(responseContent), response);
        }
        catch (Exception ex)
        {
            Logger.Log($"Exception thrown while redirecting request to {route}: {ex.Message}");
            return default;
        }
    }
}