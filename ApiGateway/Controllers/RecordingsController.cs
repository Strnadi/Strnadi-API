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
using ApiGateway.Services;
using Microsoft.AspNetCore.Mvc;
using Models.Requests;
using Shared.Communication;
using Shared.Logging;
using LogLevel = Shared.Logging.LogLevel; 

namespace ApiGateway.Controllers;

[ApiController]
[Route("recordings")]
public class RecordingsController : ControllerBase
{
    private readonly IConfiguration _configuration;

    private readonly HttpClient _httpClient;
    
    private readonly JwtService _jwtService;

    private readonly DagClient _dagClient;
    
    public RecordingsController(IConfiguration config, JwtService jwtService)
    {
        _httpClient = new HttpClient();
        _configuration = config;
        _jwtService = jwtService;
        _dagClient = new DagClient(config);
    }
    
    private string _dagCntName => _configuration["MSAddresses:DagName"] ?? throw new NullReferenceException("Failed to load microservice name");
    
    private string _dagCntPort => _configuration["MSAddresses:DagPort"] ?? throw new NullReferenceException("Failed to load microservice port");
    
    private const string dag_uploadRecPart_endpoint = "recordings/upload-part";

    [HttpGet]
    public async Task<IActionResult> GetRecordingsOfUser([FromQuery] string jwt)
    {
        if (!_jwtService.TryValidateToken(jwt, out string? email))
            return Unauthorized();

        var response = await _dagClient.GetRecordingsByEmail(email!);

        if (response.Recordings is null)
        {
            return await HandleErrorResponseAsync(response.Message);
        }

        return Ok(response.Recordings);
    }
    
    [HttpGet("download")]
    public async Task<IActionResult> Download([FromQuery] int id, [FromQuery] string jwt, [FromQuery] bool sound = false)
    {
        if (!_jwtService.TryValidateToken(jwt, out string? email))
            return Unauthorized();
        
        var response = await _dagClient.DownloadRecordingAsync(id, sound);

        if (response.Model is null)
        {
            return await HandleErrorResponseAsync(response.Message);
        }

        return Ok(response.Model);
    }

    [HttpPost("upload")]
    public async Task<IActionResult> Upload([FromBody] RecordingUploadReq request)
    {
        if (!_jwtService.TryValidateToken(request.Jwt, out string? email)) 
            return Unauthorized();

        var internalReq = request.ToInternal(email!);
        var response = await _dagClient.UploadRecordingAsync(internalReq);

        if (response.RecordingId is null)
            return await HandleErrorResponseAsync(response.Message);

        return Ok(response.RecordingId);
    }

    [HttpPost("upload-part")]
    public async Task<IActionResult> UploadPart([FromBody] RecordingPartUploadReq request)
    {
        if (!_jwtService.TryValidateToken(request.Jwt, out string? email)) 
            return Unauthorized();

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        string dagUrl = $"http://{_dagCntName}:{_dagCntPort}/{dag_uploadRecPart_endpoint}";

        try
        {
            var response = await _httpClient.PostAsync(dagUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                Logger.Log($"Recording part upload failed with status '{response.StatusCode.ToString()}'",
                    LogLevel.Warning);
                return StatusCode((int)response.StatusCode);
            }

            return Ok();
        }
        catch (Exception ex)
        {
            Logger.Log($"Exception caught while redirecting recording part uploading request to DAG: {ex.Message}", LogLevel.Error);
            return StatusCode(500, ex.Message);
        }
    }

    private async Task<IActionResult> HandleErrorResponseAsync(HttpResponseMessage response)
    {
        int statusCode = (int)response.StatusCode;
        string? content = response.Content != null!
            ? await response.Content.ReadAsStringAsync() 
            : null;
            
        return StatusCode(statusCode, content);
    }
}