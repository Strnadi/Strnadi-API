using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace Strnadi.Api.Logging;

public class CompactConsoleFormatter() : ConsoleFormatter("compact")
{
    public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
    {
        var message = logEntry.Formatter(logEntry.State, logEntry.Exception);
        if (string.IsNullOrEmpty(message) && logEntry.Exception != null)
            return;

        var level = logEntry.LogLevel switch
        {
            LogLevel.Trace => "TRC",
            LogLevel.Debug => "DBG",
            LogLevel.Information => "INF",
            LogLevel.Warning => "WRN",
            LogLevel.Error => "ERR",
            LogLevel.Critical => "CRT",
            _ => "???"
        };
        
        textWriter.Write($"[{level}] ");
        textWriter.Write(logEntry.Category.Split('.')[^1]);
        if (logEntry.Exception is not null)
            textWriter.Write($"\n{logEntry.Exception}");
        textWriter.Write('\n');
    }
}