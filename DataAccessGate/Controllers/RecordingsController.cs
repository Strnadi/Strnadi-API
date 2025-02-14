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
using DataAccessGate.Sql;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Models.Requests;
using Shared.Logging;

namespace DataAccessGate.Controllers;

[ApiController]
[Route("recordings/")]
public class RecordingsController : ControllerBase
{
    private readonly IConfiguration _configuration;
    
    private string _connectionString =>
        _configuration["ConnectionStrings:Default"] ??
        throw new NullReferenceException("Failed to upload connection string from .env file");

    public RecordingsController(IConfiguration configuration)
    {
        _configuration = configuration;
    }
    
    [HttpPost("upload")]
    public IActionResult Upload([FromBody] RecordingUploadReqInternal request)
    {
        int userId;
        
        using (var usersRepo = new UsersRepository(_connectionString)) 
            userId = usersRepo.GetUserId(request.Email);

        if (userId == -1)
            return Unauthorized();

        using var recordingsRepo = new RecordingsRepository(_connectionString);
        int recId = recordingsRepo.AddRecording(userId, request);

        if (recId != -1)
        {
            Logger.Log($"Recording '{recId}' was uploaded successfully");
            return Ok(recId);
        }
        else
        {
            Logger.Log($"Recording upload failed");
            return Conflict();
        }
    }

    [HttpPost("upload-part")]
    public IActionResult UploadPart([FromBody] RecordingPartUploadReq request)
    {
        using var repository = new RecordingsRepository(_connectionString);
        int recPartId = repository.AddRecordingPart(request);

        if (recPartId != -1)
        {
            Logger.Log($"Recording part '{recPartId}' was uploaded successfully");
            return Ok();
        }
        else
        {
            Logger.Log($"Recording part '{recPartId} uploading failed");
            return Conflict();
        }
    }
}