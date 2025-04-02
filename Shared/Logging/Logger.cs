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
namespace Shared.Logging;

public static class Logger
{
    public static void Log(string message, LogLevel level = LogLevel.Information)
    {
        Console.Write($"[{DateTime.Now}] ");
        Console.Write("[");
        Console.ForegroundColor = GetConsoleColor(level); 
        Console.Write($"{level}");
        Console.ResetColor();
        Console.Write("]");
        Console.ForegroundColor = GetConsoleColor(level);
        Console.WriteLine($" {message}"); 
        Console.ResetColor();
    }

    private static ConsoleColor GetConsoleColor(LogLevel level)
    {
        return level switch
        {
            LogLevel.Warning => ConsoleColor.Yellow,
            LogLevel.Error => ConsoleColor.Red,
            
            _ => ConsoleColor.Green
        };
    }
}

public enum LogLevel
{
    Information,
    Warning,
    Error,
}