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
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Shared.Extensions;

public static class HttpContextExtensions
{
    public static string? GetJwt(this HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue("Authorization", out StringValues authHeader)) 
            return null;
        
        var bearerToken = authHeader.ToString();
        
        return bearerToken.StartsWith("Bearer ",
            StringComparison.OrdinalIgnoreCase)
            ? bearerToken.Substring("Bearer ".Length)
                .Trim()
            : null;
    }
}