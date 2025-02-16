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
    private IConfiguration _configuration;
    private string _connectionString => _configuration["ConnectionStrings:Default"] ?? throw new NullReferenceException("Failed to upload connection string from .env file");
    
    public UsersController(IConfiguration configuration)
    {
        _configuration = configuration;
    }
    
    [HttpPost("/users/authorize-user")]
    public IActionResult AuthorizeUser(LoginRequest request)
    { 
        using var repository = new UsersRepository(_connectionString);
        bool authorized = repository.AuthorizeUser(request.Email, request.Password);
        
        return authorized ?
            Ok() :
            Unauthorized();
    }

    [HttpPost("/users/sign-up")]
    public IActionResult SignUp([FromBody] SignUpRequest request)
    {
        using var repository = new UsersRepository(_connectionString);
        if (repository.AddUser(request))
        {
            return Ok();
        }
        else
        {
            return Conflict();
        }
    }

    [HttpPost("/users/verify")]
    public IActionResult VerifyUser([FromQuery] string email)
    {
        using var repository = new UsersRepository(_connectionString);

        if (repository.Verify(email))
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
    public IActionResult Get(string email)
    {
        using var repository = new UsersRepository(_connectionString);
        
        User? user = repository.GetUser(email);
        
        return user is not null ? 
            Ok(user) : 
            NotFound();
    }
}