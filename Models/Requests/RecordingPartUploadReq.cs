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

public class RecordingPartUploadReq
{
    public string Jwt { get; set; }
    
    public int RecordingId { get; set; }
    
    public DateTime Start { get; set; }
    
    public DateTime End { get; set; }

    public decimal LatitudeStart { get; set; }

    public decimal LatitudeEnd { get; set; }
    
    public decimal LongitudeStart { get; set; }
    
    public decimal LongitudeEnd { get; set; }
    
    public string Data { get; set; }
}