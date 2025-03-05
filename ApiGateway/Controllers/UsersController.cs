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
using ApiGateway.Services;
using Microsoft.AspNetCore.Mvc;
using Models.Database;
using Models.Requests;
using Shared.Communication;
using Shared.Extensions;
using Shared.Logging;
using LogLevel = Shared.Logging.LogLevel;

namespace ApiGateway.Controllers;

[ApiController]
[Route("/users")]
public class UsersController : ControllerBase
{
    private readonly DagUsersControllerClient _dagClient;
    
    private readonly JwtService _jwtService;
    
    public UsersController(JwtService jwtService, DagUsersControllerClient client)
    {
        _jwtService = jwtService;
        _dagClient = client;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        string? jwt = this.GetJwt();

        if (jwt is null)
            return BadRequest("No JWT provided");
        
        if (!_jwtService.TryValidateToken(jwt, out string? email))
            return Unauthorized();

        HttpRequestResult<UserModel?>? response = await _dagClient.GetUser(email!);

        if (response?.Value is null)
            return await this.HandleErrorResponseAsync(response);

        return Ok(response.Value);
    } 
}