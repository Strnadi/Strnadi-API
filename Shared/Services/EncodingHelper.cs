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

using System.Security.Cryptography;
using System.Text;

namespace Shared.Services;

public static class EncodingHelper
{
    public static byte[] DecodeFromBase64(string encoded)
    {
        return Convert.FromBase64String(encoded);
    }

    public static string EncodeToBase64(byte[] binary)
    {
        return Convert.ToBase64String(binary);
    }

    public static string Sha256(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}