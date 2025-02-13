using DataAccessGate.Sql;
using Microsoft.AspNetCore.Mvc;
using Models.Requests;

namespace DataAccessGate.Controllers;

[ApiController]
[Route("recordings")]
public class RecordingsController
{
    private readonly IConfiguration _configuration;
    
    private string _connectionString =>
        _configuration["ConnectionStrings:Default"] ??
        throw new NullReferenceException("Failed to upload connection string from .env file");

    public RecordingsController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpPost("recordings/upload")]
    public async Task<IActionResult> Upload([FromBody] RecordingUploadReq request)
    {
        using var repository = new RecordingsRepository(_connectionString);
        repository.AddRecording(request);

        // must return id of generated recording
    }

    [HttpPost("recordings/upload-part")]
    public async Task<IActionResult> UploadPart([FromBody] RecordingUploadReq request)
    {
        
    }
}