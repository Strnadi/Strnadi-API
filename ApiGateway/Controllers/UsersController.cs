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
using Shared.Logging;
using LogLevel = Shared.Logging.LogLevel;

namespace ApiGateway.Controllers;

[ApiController]
[Route("/users")]
public class UsersController : ControllerBase
{
    private readonly IConfiguration _configuration;

    private readonly HttpClient _httpClient;
    
    private readonly JwtService _jwtService;
    
    private string _dagCntName => _configuration["MSAddresses:DagName"] ?? throw new NullReferenceException("Failed to load microservice name");
    private string _dagCntPort => _configuration["MSAddresses:DagPort"] ?? throw new NullReferenceException("Failed to load microservice port");
    
    private const string dag_get_endpoint = "users";
    
    public UsersController(IConfiguration config)
    {
        _configuration = config;
        _httpClient = new HttpClient();
        _jwtService = new JwtService(config);
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string jwt)
    {
        if (!_jwtService.TryValidateToken(jwt, out string? email))
            return Unauthorized();

        string dagUrl = $"http://{_dagCntName}:{_dagCntPort}/{dag_get_endpoint}/{email}";

        try
        {
            var response = await _httpClient.GetAsync(dagUrl);
            return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            Logger.Log($"Exception caught while communicating with DAG module: {ex.Message}", LogLevel.Error);
            return StatusCode(500, ex.Message);
        }
    } 
}