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

using System.Text.Json;
using DataAccessGate.Sql;
using Microsoft.AspNetCore.Mvc;
using Models.Database;
using Models.Requests;
using Shared.Logging;

namespace DataAccessGate.Controllers;

[ApiController]
[Route("recordings/")]
public class RecordingsController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public RecordingsController(IConfiguration configuration)
    {
        _configuration = configuration;
    }
    
    [HttpGet]
    public IActionResult GetByEmail([FromQuery] string email,
        [FromQuery] int count,
        [FromServices] RecordingsRepository recordingsRepo,
        [FromServices] UsersRepository usersRepo)
    {
        if (!usersRepo.TryGetUserId(email, out int userId))
            return BadRequest("Invalid email");
        
        var recording = recordingsRepo.GetUsersRecordings(userId, count);

        if (recording is null)
            return StatusCode(500);

        return Ok(recording);
    }

    [HttpGet("{recordingId:int}/download")]
    public IActionResult Download(int recordingId,
        [FromQuery] bool sound,
        [FromServices] RecordingsRepository repository)
    {
        RecordingModel? recording = repository.GetRecording(recordingId);
        
        if (recording == null)
        {
            Logger.Log($"Recording {recordingId} not found for downloading.");
            return NotFound();
        }

        recording.Parts = repository.GetRecordingParts(recordingId, sound);

        Logger.Log($"Recording {recording.Id} was sent to download.");
        return Ok(recording);
    }
    
    [HttpPost("upload")]
    public IActionResult Upload([FromBody] RecordingUploadReqInternal request,
        [FromServices] UsersRepository usersRepo,
        [FromServices] RecordingsRepository recordingsRepo)
    {
        if (!usersRepo.TryGetUserId(request.Email, out int userId))
            return Unauthorized();

        int recId = recordingsRepo.AddRecording(userId, request);

        if (recId != -1)
        {
            Logger.Log($"Recording '{recId}' was uploaded successfully");
            return Ok(recId);
        }
        else
        {
            Logger.Log("Recording upload failed");
            return Conflict();
        }
    }

    [HttpPost("upload-part")]
    public IActionResult UploadPart([FromBody] RecordingPartUploadReq request,
        [FromServices] RecordingsRepository repository)
    {
        int recPartId = repository.AddRecordingPart(request);

        if (recPartId != -1)
        {
            Logger.Log($"Recording part '{recPartId}' was uploaded successfully");
            return Ok(recPartId);
        }
        else
        {
            Logger.Log("Recording part uploading failed");
            return Conflict();
        }
    }

    [HttpPatch("{recordingId:int}/modify")]
    public IActionResult Modify(int recordingId,
        [FromBody] RecordingModel model,
        [FromServices] RecordingsRepository repository)
    {
        if (!repository.ExistsRecording(recordingId))
            return NotFound($"Recording '{recordingId}' not found");
        
        bool success = repository.ModifyRecording(recordingId, model);
        Logger.Log($"Recording {recordingId} modification " + (success ? "succeeded" : "failed"));
        
        return success ?
            Ok() :
            Conflict();
    }

    [HttpGet("filtered")]
    public IActionResult GetFiltered([FromServices] RecordingsRepository repo)
    {
        var filteredRecordings = repo.GetFilteredRecordings();

        if (filteredRecordings is null)
            return StatusCode(500, "Failed to get filtered recordings");
        
        
        return Ok(filteredRecordings);
    }
}