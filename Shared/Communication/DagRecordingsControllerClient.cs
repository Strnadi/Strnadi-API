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
    private string _dagCntName => Configuration["MSAddresses:DagName"] ?? throw new NullReferenceException("Failed to load microservice name");
    
    private string _dagCntPort => Configuration["MSAddresses:DagPort"] ?? throw new NullReferenceException("Failed to load microservice port");

    public DagRecordingsControllerClient(IConfiguration configuration, HttpClient httpClient) : base(configuration, httpClient)
    {
    }
    
    public async Task<RedirectResult<int?>?> UploadAsync(RecordingUploadReqInternal internalReq)
    {
        string url = GetUploadUrl();
        
        RedirectResult<string>? response = await PostAsync<RecordingUploadReqInternal, string>(url, internalReq);
        
        return response is null
            ? null
            : new RedirectResult<int?>(response.Value is not null
                    ? int.Parse(response.Value)
                    : null,
                response.Message);
    }
    
    public async Task<RedirectResult<RecordingModel?>?> DownloadAsync(int recordingId, bool sound) 
    {
        string url = GetDownloadUrl(recordingId, sound);
        return await GetAsync<RecordingModel?>(url);
    }
    
    public async Task<RedirectResult<IEnumerable<RecordingModel>>?> GetByEmailAsync(string email)
    {
        string url = GetRecordingUrl(email);
        return await GetAsync<IEnumerable<RecordingModel>>(url);
    }
    
    public async Task<RedirectResult<int?>> UploadPartAsync(RecordingPartUploadReq request)
    {
        throw new NotImplementedException();
    }
    
    private string GetRecordingUrl(string email) =>
        $"http://{_dagCntName}:{_dagCntPort}/recordings?email={email}";
    
    private string GetDownloadUrl(int recordingId, bool sound) =>
        $"http://{_dagCntName}:{_dagCntPort}/recordings/download?id={recordingId}&sound={sound}";

    private string GetUploadUrl() =>
        $"http://{_dagCntName}:{_dagCntPort}/recordings/upload";
}