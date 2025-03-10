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
using Microsoft.AspNetCore.Mvc;
using Models.Database;
using Models.Requests;
using Shared.Logging;
using LoginRequest = Microsoft.AspNetCore.Identity.Data.LoginRequest;

namespace DataAccessGate.Controllers;

[ApiController]
[Route("/users")]
public class UsersController : ControllerBase
{
    [HttpPost("/users/authorize-user")]
    public IActionResult AuthorizeUser([FromBody] LoginRequest request, [FromServices] UsersRepository repository)
    { 
        bool authorized = repository.AuthorizeUser(request.Email, request.Password);
        
        return authorized ?
            Ok() :
            Unauthorized();
    }

    [HttpPost("/users/sign-up")]
    public IActionResult SignUp([FromBody] SignUpRequest request, [FromServices] UsersRepository repository)
    {
        bool added = repository.AddUser(request);
        repository.Dispose();
        
        if (added)
        {
            return Ok();
        }
        else
        {
            return Conflict();
        }
    }

    [HttpPost("{email}/verify")]
    public IActionResult VerifyUser(string email, [FromServices] UsersRepository repository)
    {
        bool verified = repository.Verify(email);
        repository.Dispose();
        
        if (verified)
        {
            Logger.Log($"User '{email}' verified successfully");
            return Ok();
        }
        else
        {
            return StatusCode(500);
        }
    }

    [HttpGet("{email}")]
    public IActionResult Get(string email, [FromServices] UsersRepository repository)
    {
        UserModel? user = repository.GetUser(email);
        repository.Dispose();
        
        return user is not null ? 
            Ok(user) : 
            NotFound("User does not exist.");
    }

    [HttpGet("{email}/is-admin")]
    public IActionResult IsAdmin(string email, [FromServices] UsersRepository repository)
    {
        if (!repository.ExistsUser(email))
            return NotFound($"User '{email}' does not exist.");
        
        bool? isAdmin = repository.IsAdmin(email);

        return isAdmin is not null ? 
            Ok(isAdmin) : 
            StatusCode(500, "Failed to check if user is admin.");
    }

    [HttpPatch("{email}/change-password")]
    public IActionResult ChangePassword(string email,
        [FromQuery] string newPassword,
        [FromServices] UsersRepository repo)
    {
        if (string.IsNullOrWhiteSpace(newPassword))
            return BadRequest("New password cannot be empty.");
        
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest("Email cannot be empty.");
        
        if (!repo.ExistsUser(email))
            return NotFound($"User '{email}' does not exist.");
        
        bool? changed = repo.ChangePassword(email, newPassword);
        
        if (changed is null)
            return StatusCode(500, "Failed to change password.");
        
        return changed.Value ? 
            Ok() : 
            StatusCode(401, "Failed to change password.");
    }
}