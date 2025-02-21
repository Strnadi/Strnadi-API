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
using Shared.Communication;
using Shared.Logging;

namespace Shared.Extensions;

public static class ControllerExtensions
{
    public static async Task<IActionResult> HandleErrorResponseAsync(this ControllerBase controller, IHttpRequestResult? httpRequestResult) =>
        await HandleErrorResponseAsync(controller, httpRequestResult?.Message);
    
    public static async Task<IActionResult> HandleErrorResponseAsync(this ControllerBase controller, HttpResponseMessage? response)
    {
        if (response is null)
            return controller.StatusCode(500);

        Logger.Log($"Operation {controller.HttpContext.Request.Path} failed with status code " + (int)response.StatusCode);
        
        int statusCode = (int)response.StatusCode;
        string? content = response.Content != null!
            ? await response.Content.ReadAsStringAsync() 
            : null;
        
        return controller.StatusCode(statusCode, content);
    }

    public static string? GetJwt(this ControllerBase controller) => controller.HttpContext.GetJwt();
}