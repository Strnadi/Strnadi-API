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

using Microsoft.Extensions.Configuration;

namespace Shared.Tools;

public class LinkGenerator
{
    private readonly IConfiguration _configuration;

    private string _webRootPath => _configuration["Web"];

    public LinkGenerator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateVerificationLink(int userId, string jwt)
    {
        string host = _configuration["Host"] ?? throw new NullReferenceException("Failed to get Host from configuration");
        string link = $"{host}/users/{userId}/verify-email?jwt={jwt}";

        return link;
    }

    public string GenerateEmailVerificationRedirectionLink(bool success)
    {
        return $"{_webRootPath}/ucet/email-{(success ? "" : "ne")}overen";
    }

    public string GeneratePasswordResetLink(int userId, string jwt)
    {
        return $"{_webRootPath}/ucet/obnova-hesla?token={jwt}&userId={userId}";
    }

    public string GenerateAchievementImageUrl(int id)
    {
        string host = _configuration["Host"] ?? throw new NullReferenceException("Failed to get Host from configuration");
        string link = $"{host}/achievements/{id}/photo";
        return link;
    }
}