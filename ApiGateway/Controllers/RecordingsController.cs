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
using Microsoft.AspNetCore.Mvc.Diagnostics;
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
    
    private readonly JwtService _jwtService;

    private readonly DagRecordingsControllerClient _dagClient;
    
    public RecordingsController(IConfiguration config, JwtService jwtService, DagRecordingsControllerClient client)
    {
        _configuration = config;
        _jwtService = jwtService;
        _dagClient = client;
    }

    [HttpGet]
    public async Task<IActionResult> GetRecordingsOfUser([FromQuery] string jwt)
    {
        if (!_jwtService.TryValidateToken(jwt, out string? email))
            return Unauthorized();

        var response = await _dagClient.GetByEmailAsync(email!);

        if (response?.Value is null)
        {
            return await HandleErrorResponseAsync(response);
        }

        return Ok(response.Value);
    }
    
    [HttpGet("download")]
    public async Task<IActionResult> Download([FromQuery] int id, [FromQuery] string jwt, [FromQuery] bool sound = false)
    {
        if (!_jwtService.TryValidateToken(jwt, out string? email))
            return Unauthorized();
        
        var response = await _dagClient.DownloadAsync(id, sound);

        if (response?.Value is null)
        {
            return await HandleErrorResponseAsync(response);
        }

        return Ok(response.Value);
    }

    [HttpPost("upload")]
    public async Task<IActionResult> Upload([FromBody] RecordingUploadReq request)
    {
        if (!_jwtService.TryValidateToken(request.Jwt, out string? email)) 
            return Unauthorized();

        var internalReq = request.ToInternal(email!);
        var response = await _dagClient.UploadAsync(internalReq);

        if (response?.Value is null)
            return await HandleErrorResponseAsync(response);

        return Ok(response.Value);
    }

    [HttpPost("upload-part")]
    public async Task<IActionResult> UploadPart([FromBody] RecordingPartUploadReq request)
    {
        if (!_jwtService.TryValidateToken(request.Jwt, out string? email)) 
            return Unauthorized();

        var response = await _dagClient.UploadPartAsync(request);

        if (response?.Value is null)
            return await HandleErrorResponseAsync(response);

        return Ok(response.Value);

        // var json = JsonSerializer.Serialize(request);
        // var content = new StringContent(json, Encoding.UTF8, "application/json");
        //
        // string dagUrl = $"http://{_dagCntName}:{_dagCntPort}/{dag_uploadRecPart_endpoint}";
        //
        // try
        // {
        //     var response = await _httpClient.PostAsync(dagUrl, content);
        //
        //     if (!response.IsSuccessStatusCode)
        //     {
        //         Logger.Log($"Recording part upload failed with status '{response.StatusCode.ToString()}'",
        //             LogLevel.Warning);
        //         return StatusCode((int)response.StatusCode);
        //     }
        //
        //     return Ok();
        // }
        // catch (Exception ex)
        // {
        //     Logger.Log($"Exception caught while redirecting recording part uploading request to DAG: {ex.Message}", LogLevel.Error);
        //     return StatusCode(500, ex.Message);
        // }
    }

    private async Task<IActionResult> HandleErrorResponseAsync(IRedirectResult? response)
    {
        if (response is null)
            return StatusCode(500);
        
        HttpResponseMessage message = response.Message;
        int statusCode = (int)message.StatusCode;
        string? content = message.Content != null!
            ? await message.Content.ReadAsStringAsync() 
            : null;
            
        return StatusCode(statusCode, content);
    }
}