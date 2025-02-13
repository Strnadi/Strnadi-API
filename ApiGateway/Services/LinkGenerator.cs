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
using ApiGateway.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Services;

public class LinkGenerator
{
    public string GenerateLink(string jwt, HttpContext httpContext, ControllerContext controllerContext)
    {
        string scheme = httpContext.Request.Scheme;
        string host = httpContext.Request.Host.ToUriComponent();
        string route = controllerContext.ActionDescriptor.AttributeRouteInfo!.Template!;
        
        string link = $"{scheme}://{host}{route}?jwt={jwt}";

        return link;
    }
}