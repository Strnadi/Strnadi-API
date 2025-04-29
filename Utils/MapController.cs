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
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Utils;

[ApiController]
[Route("map")]
public class MapController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public MapController(IConfiguration configuration)
    {
        _configuration = configuration;
    }
    
    [HttpGet("{*path}")]
    public async Task<IActionResult> ForwardToMapyCz([FromRoute] string path)
    {
        var query = Request.QueryString.Value;
        var targetUrl = $"https://api.mapy.cz/{path}{query}";

        using var client = new HttpClient();

        var req = new HttpRequestMessage(HttpMethod.Get, targetUrl);
        req.Headers.Add("X-Mapy-Api-Key", _configuration["MapyCzKey"]);

        var res = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        Response.RegisterForDispose(res);

        if (!res.IsSuccessStatusCode)
        {
            return StatusCode((int)res.StatusCode, await res.Content.ReadAsStringAsync());
        }

        var stream = await res.Content.ReadAsStreamAsync();
        var contentType = res.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";

        return new FileStreamResult(stream, contentType);
    }
}