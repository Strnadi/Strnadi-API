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

using System.ComponentModel.DataAnnotations.Schema;
using Shared.Models.Database.Dialects;

namespace Shared.Models.Requests.Recordings;

public class FilteredRecordingPartModel
{
    public int Id { get; set; }

    public DateTime StartDate { get; set; }
    
    public DateTime EndDate { get; set; }

    public short State { get; set; }

    public string? DialectCode { get; set; }

    public int RecordingId { get; set; }
    
    [NotMapped]
    public List<DetectedDialectModel> DetectedDialects { get; set; }

    public FilteredRecordingPartModel()
    {
        DetectedDialects = [];
    }
}