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
using Models.Database.Enums;

namespace Models.Database;

public class User
{
    public int Id { get; set; }
    
    public UserRole Role { get; set; }

    public string? Nickname { get; set; }

    public string Email { get; set; }
    
    public string Password { get; set; }
    
    public string FirstName { get; set; } 

    public string LastName { get; set; }

    public DateTime CreationDate { get; set; } 

    public bool? IsEmailVerified { get; set; }

    public bool? Consent { get; set; }
}