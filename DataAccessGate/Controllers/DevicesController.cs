using DataAccessGate.Sql;
using Microsoft.AspNetCore.Mvc;
using Models.Requests;
using Shared.Logging;

namespace DataAccessGate.Controllers;

[ApiController]
[Route("devices")]
public class DevicesController : ControllerBase
{
    [HttpPost("{email}/device")]
    public IActionResult Device(string email,
        [FromBody] DeviceRequest request,
        [FromServices] UsersRepository usersRepo,
        [FromServices] DevicesRepository devicesRepo)
    {
        if (request.OldToken is not null && request.NewToken is null)
        {
            devicesRepo.DeleteDevice(request.OldToken);
            Logger.Log($"Device deleted: {request.OldToken}");
        }
        else if (request.OldToken is null && request.NewToken is not null)
        {
            int userId = usersRepo.GetUserId(email);
            devicesRepo.AddDevice(userId, request.NewToken, request.DeviceName, request.DevicePlatform);
            Logger.Log($"Device added: {request.OldToken} for user {email}");
        }
        else if (request.OldToken is not null && request.NewToken is not null)
        {
            devicesRepo.UpdateDevice(request.OldToken, request.NewToken);
            Logger.Log($"Device updated: {request.OldToken} -> {request.NewToken}");
        }
        else
        {
            Logger.Log("Device modification failed: both old and new tokens are null");
            return BadRequest();
        }

        return Ok();
    }
}