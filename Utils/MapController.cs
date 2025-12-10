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
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Utils;

[ApiController]
[Route("map")]
public class MapController : ControllerBase
{
    private readonly IConfiguration _configuration;

    // Shared handler and client for the lifetime of the application:
    // - reuse connections, TLS sessions
    // - tune max connections and pooled lifetime
    private static readonly SocketsHttpHandler _socketsHandler = new SocketsHttpHandler
    {
        // IMPORTANT: don't enable AutomaticDecompression if you want to forward compressed bytes "as is".
        AutomaticDecompression = DecompressionMethods.None,
        MaxConnectionsPerServer = 100,                 // tune as needed
        PooledConnectionLifetime = TimeSpan.FromMinutes(10),
        UseCookies = false,
        AllowAutoRedirect = false
    };

    private static readonly HttpClient _httpClient = new HttpClient(_socketsHandler, disposeHandler: false)
    {
        // We don't set BaseAddress because path varies
        Timeout = TimeSpan.FromSeconds(100) // tune if necessary
    };

    public MapController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet("{*path}")]
    public async Task ForwardToMapyCz([FromRoute] string path)
    {
        var targetBase = "https://api.mapy.cz/";
        var query = Request.QueryString.HasValue ? Request.QueryString.Value : string.Empty;
        var targetUrl = new Uri(targetBase + path + query);

        using var upstreamReq = new HttpRequestMessage(HttpMethod.Get, targetUrl);

        // Forward a set of helpful request headers from the client:
        // - Accept, Accept-Encoding (so origin can compress)
        // - If-None-Match / If-Modified-Since (so origin can reply 304)
        if (Request.Headers.TryGetValue("Accept", out var accept))
            upstreamReq.Headers.TryAddWithoutValidation("Accept", (string[])accept);
        if (Request.Headers.TryGetValue("Accept-Encoding", out var enc))
            upstreamReq.Headers.TryAddWithoutValidation("Accept-Encoding", (string[])enc);
        if (Request.Headers.TryGetValue("If-None-Match", out var inm))
            upstreamReq.Headers.TryAddWithoutValidation("If-None-Match", (string[])inm);
        if (Request.Headers.TryGetValue("If-Modified-Since", out var ims))
            upstreamReq.Headers.TryAddWithoutValidation("If-Modified-Since", (string[])ims);


        // Add API key
        upstreamReq.Headers.TryAddWithoutValidation("X-Mapy-Api-Key", _configuration["MapyCzKey"]);

        // Ask for headers only first to stream body
        using var upstreamResp = await _httpClient.SendAsync(upstreamReq, HttpCompletionOption.ResponseHeadersRead);

        // Mirror status code
        Response.StatusCode = (int)upstreamResp.StatusCode;

        // Hop-by-hop headers that must not be forwarded
        string[] hopByHop = new[]
        {
            "Connection", "Keep-Alive", "Proxy-Authenticate", "Proxy-Authorization",
            "TE", "Trailer", "Transfer-Encoding", "Upgrade"
        };

        // Copy response headers (excluding hop-by-hop)
        foreach (var header in upstreamResp.Headers)
        {
            if (hopByHop.Contains(header.Key, StringComparer.OrdinalIgnoreCase)) continue;
            Response.Headers[header.Key] = header.Value.ToArray();
        }

        foreach (var header in upstreamResp.Content.Headers)
        {
            if (hopByHop.Contains(header.Key, StringComparer.OrdinalIgnoreCase)) continue;
            Response.Headers[header.Key] = header.Value.ToArray();
        }

        // Explicitly set Content-Type and Content-Length if available
        if (upstreamResp.Content.Headers.ContentType != null)
            Response.ContentType = upstreamResp.Content.Headers.ContentType.ToString();

        if (upstreamResp.Content.Headers.ContentLength.HasValue)
            Response.ContentLength = upstreamResp.Content.Headers.ContentLength.Value;

        // IMPORTANT: some servers return Transfer-Encoding: chunked and ASP.NET Core manages that itself.
        // We've already copied headers above, but don't copy Transfer-Encoding. Ensure it's not present:
        Response.Headers.Remove("Transfer-Encoding");

        // Stream the body to the client (preserves bytes exactly as received)
        await upstreamResp.Content.CopyToAsync(Response.Body);
        // Don't call return; we've already written to the response.
    }
}