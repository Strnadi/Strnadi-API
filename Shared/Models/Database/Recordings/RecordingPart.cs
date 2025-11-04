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
namespace Shared.Models.Database.Recordings;

public class RecordingPart
{
    public int Id { get; set; }
    
    public int RecordingId { get; set; }
 
    public DateTime StartDate { get; set; }
    
    public DateTime EndDate { get; set; }
    
    public decimal GpsLatitudeStart { get; set; }
    
    public decimal GpsLatitudeEnd { get; set; }
    
    public decimal GpsLongitudeStart { get; set; }
    
    public decimal GpsLongitudeEnd { get; set; }

    public string? Square { get; set; }

    public string? FilePath { get; set; }

    public string? DataBase64 { get; set; }
}