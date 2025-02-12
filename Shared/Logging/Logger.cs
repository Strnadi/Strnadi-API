namespace Shared.Logging;

public static class Logger
{
    public static void Log(string message, LogLevel level = LogLevel.Information)
    {
        Console.Write($"[{DateTime.Now}] ");
        Console.ForegroundColor = GetConsoleColor(level);
        Console.Write($"[{level.ToString()}] ");
        Console.Write($"{message} \n");
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