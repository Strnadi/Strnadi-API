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
    public async Task<IActionResult> GetRecordingsOfUser()
    {
        string? jwt = this.GetJwt();

        if (jwt is null)
            return BadRequest("No JWT provided");
        
        if (!_jwtService.TryValidateToken(jwt, out string? email))
            return Unauthorized();

        var response = await _dagClient.GetByEmailAsync(email!);

        if (response?.Value is null)
        {
            return await this.HandleErrorResponseAsync(response);
        }

        return Ok(response.Value);
    }
    
    [HttpGet("download")]
    public async Task<IActionResult> Download([FromQuery] int id, [FromQuery] bool sound = false)
    {
        string? jwt = this.GetJwt();
        
        if (jwt is null)
            return BadRequest("No JWT provided");
        
        if (!_jwtService.TryValidateToken(jwt, out _))
            return Unauthorized();
        
        var response = await _dagClient.DownloadAsync(id, sound);

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
}