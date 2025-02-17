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

    [HttpPost("/users/verify")]
    public IActionResult VerifyUser([FromQuery] string email, [FromServices] UsersRepository repository)
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
}