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

public class DagRecordingsControllerClient : ServiceClient
{
    private readonly IConfiguration _configuration;
    
    private string _dagCntName => _configuration["MSAddresses:DagName"] ?? throw new NullReferenceException("Failed to load microservice name");
    
    private string _dagCntPort => _configuration["MSAddresses:DagPort"] ?? throw new NullReferenceException("Failed to load microservice port");
    
    public DagRecordingsControllerClient(IConfiguration configuration)
    {
        _configuration = configuration;
    }
    
    public async Task<(int? RecordingId, HttpResponseMessage Message)> UploadAsync(RecordingUploadReqInternal internalReq)
    {
        string url = GetUploadUrl();
        
        (string? RecordingIdStr, HttpResponseMessage Message) 
            response = await PostAsync<RecordingUploadReqInternal, string>(url, internalReq);
        
        return (response.RecordingIdStr is not null
            ? int.Parse(response.RecordingIdStr)
            : null, 
            response.Message);
    }
    
    public async Task<(RecordingModel? Model, HttpResponseMessage Message)> 
        DownloadAsync(int recordingId, bool sound) 
    {
        string url = GetDownloadUrl(recordingId, sound);
        return await GetAsync<RecordingModel>(url);
    }
    
    public async Task<(IEnumerable<RecordingModel>? Recordings, HttpResponseMessage Message)> 
        GetByEmailAsync(string email)
    {
        string url = GetRecordingUrl(email);
        return await GetAsync<IEnumerable<RecordingModel>>(url);
    }
    
    private string GetRecordingUrl(string email) =>
        $"http://{_dagCntName}:{_dagCntPort}/recordings?email={email}";
    
    private string GetDownloadUrl(int recordingId, bool sound) =>
        $"http://{_dagCntName}:{_dagCntPort}/recordings/download?id={recordingId}&sound={sound}";

    private string GetUploadUrl() =>
        $"http://{_dagCntName}:{_dagCntPort}/recordings/upload";
}