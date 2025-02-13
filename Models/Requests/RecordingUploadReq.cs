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
namespace Models.Requests;

public class RecordingUploadReq
{
    public string Jwt { get; set; }
    
    public DateTime CreatedAt { get; set; }

    public short EstimatedBirdsCount { get; set; }

    public string Device { get; set; }

    public bool ByApp { get; set; }
    
    public string? Note { get; set; }

    public RecordingUploadReqInternal ToInternal(string email)
    {
        return new RecordingUploadReqInternal()
        {
            Email = email,
            CreatedAt = this.CreatedAt,
            EstimatedBirdsCount = this.EstimatedBirdsCount,
            Device = this.Device,
            ByApp = this.ByApp,
            Note = this.Note,
        };
    }
}