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
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Shared.Extensions;

public static class ControllerExtensions
{
    public static string? GetJwt(this ControllerBase controller) => controller.HttpContext.GetJwt();
    
    public static async Task<string> GetRequestStringAsync(this ControllerBase controller)
    {
        var request = controller.HttpContext.Request;
        request.EnableBuffering();

        var body = string.Empty;
        if (request.Body.CanSeek)
        {
            request.Body.Seek(0, SeekOrigin.Begin);
            using (var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true))
            {
                body = await reader.ReadToEndAsync();
                request.Body.Seek(0, SeekOrigin.Begin);
            }
        }

        var headers = string.Join(Environment.NewLine, request.Headers.Select(h => $"{h.Key}: {h.Value}"));
        var requestString = $"{request.Method} {request.Path}{request.QueryString}{Environment.NewLine}{headers}{Environment.NewLine}{body}";

        return requestString;
    } 
}