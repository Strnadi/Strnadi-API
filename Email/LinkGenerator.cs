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
using Microsoft.Extensions.Configuration;

namespace Email;

public class LinkGenerator
{
    private readonly IConfiguration _configuration;

    public LinkGenerator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateVerificationLink(HttpContext context, string jwt)
    {
        string scheme = context.Request.IsHttps ? "https" : "http";
        
        string link = $"{scheme}://{context.Request.Host}/auth/verify?jwt={jwt}";

        return link;
    }

    public string GenerateEmailVerificationRedirectionLink(bool success)
    {
        return $"https://registration.strnadi.cz?success={success}";
    }

    public string GeneratePasswordResetLink(string jwt)
    {
        return $"https://registration.strnadi.cz/forgotten-password?jwt={jwt}";
    }
}