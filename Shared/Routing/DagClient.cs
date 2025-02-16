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

namespace Shared.Routing;

public class DagClient : ServiceClient
{
    private readonly IConfiguration _configuration;
    
    private string _dagCntName => _configuration["MSAddresses:DagName"] ?? throw new NullReferenceException("Failed to load microservice name");
    
    private string _dagCntPort => _configuration["MSAddresses:DagPort"] ?? throw new NullReferenceException("Failed to load microservice port");
    
    private const string dag_uploadRec_endpoint = "recordings/upload";
    
    private const string dag_uploadRecPart_endpoint = "recordings/upload-part";
    
    public DagClient(IConfiguration configuration)
    {
        _configuration = configuration;
    }
    
    public async Task<(RecordingModel? Model, HttpResponseMessage Message)> DownloadAsync(int recordingId, bool sound)
    {
        string url = GetDownloadUrl(recordingId, sound);
        
        return await GetAsync<RecordingModel>(url);
    }

    private string GetDownloadUrl(int recordingId, bool sound) => $"http://{_dagCntName}:{_dagCntPort}/download?id={recordingId}&sound={sound}";
}