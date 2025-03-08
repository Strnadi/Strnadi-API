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

using ApiGateway.Services;
using Microsoft.AspNetCore.Mvc;
using Models.Database;
using Models.Requests;
using Shared.Communication;
using Shared.Extensions;

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
    public async Task<IActionResult> GetRecordingsOfUser([FromQuery] int count = 0)
    {
        string? jwt = this.GetJwt();

        if (count < 0)
            return BadRequest("Invalid recordings count");

        if (jwt is null)
            return BadRequest("No JWT provided");
        
        if (!_jwtService.TryValidateToken(jwt, out string? email))
            return Unauthorized();

        var response = await _dagClient.GetByEmailAsync(email!, count);

        if (response?.Value is null)
            return await this.HandleErrorResponseAsync(response);

        return Ok(response.Value);
    }

    [HttpPatch("{recordingId}/modify")]
    public async Task<IActionResult> Modify(int recordingId, [FromBody] RecordingModel model, [FromServices] DagUsersControllerClient usersClient)
    {
        string? jwt = this.GetJwt();
        
        if (jwt is null)
            return BadRequest("No JWT provided");

        if (!_jwtService.TryValidateToken(jwt, out string? email))
            return Unauthorized();

        var isAdminResponse = await usersClient.IsAdminAsync(email!);
        if (isAdminResponse?.Value is null)
            return await this.HandleErrorResponseAsync(isAdminResponse);
        
        var modifyResponse = await _dagClient.ModifyAsync(recordingId, model);
        
        if (modifyResponse is null)
            return await this.HandleErrorResponseAsync(modifyResponse);

        if (!modifyResponse.Success)
            return StatusCode((int)modifyResponse.StatusCode);

        return Ok();
    }
    
    [HttpGet("{recordingId:int}/download")]
    public async Task<IActionResult> Download(int recordingId, [FromQuery] bool sound = false)
    {
        string? jwt = this.GetJwt();
        
        if (jwt is null)
            return BadRequest("No JWT provided");
        
        if (!_jwtService.TryValidateToken(jwt, out _))
            return Unauthorized();
        
        var response = await _dagClient.DownloadAsync(recordingId, sound);

        if (response?.Value is null)
        {
            return await this.HandleErrorResponseAsync(response);
        }

        return Ok(response.Value);
    }

    [HttpPost("upload")]
    public async Task<IActionResult> Upload([FromBody] RecordingUploadReq request)
    {
        string? jwt = this.GetJwt();
        
        if (jwt is null) 
            return BadRequest("No JWT provided");
        
        if (!_jwtService.TryValidateToken(jwt, out string? email)) 
            return Unauthorized();

        var internalReq = request.ToInternal(email!);
        var response = await _dagClient.UploadAsync(internalReq);

        if (response?.Value is null)
            return await this.HandleErrorResponseAsync(response);

        return Ok(response.Value);
    }

    [HttpPost("upload-part")]
    public async Task<IActionResult> UploadPart([FromBody] RecordingPartUploadReq request)
    {
        string? jwt = this.GetJwt();

        if (jwt is null)
            return BadRequest("No JWT provided");
        
        if (!_jwtService.TryValidateToken(jwt, out string? email)) 
            return Unauthorized();

        var response = await _dagClient.UploadPartAsync(request);

        if (response?.Value is null)
            return await this.HandleErrorResponseAsync(response);

        return Ok(response.Value);
    }

    [HttpGet("filtered")]
    public async Task<IActionResult> GetFiltered([FromServices] RecordingsControllerClient client)
    {
        var response = await client.GetFilteredSubrecordingsAsync();

        if (response?.Value is null)
            return await this.HandleErrorResponseAsync(response);

        return Ok(response.Value);
    }
}