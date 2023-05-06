using FreneticUtilities.FreneticToolkit;
using StableUI.Core;

namespace StableUI.Utils;

/// <summary>Central internal logging handler.</summary>
public static class Logs
{
    /// <summary>Thread lock to prevent messages overlapping.</summary>
    public static LockObject ConsoleLock = new();

    /// <summary>Log an initialization-related message.</summary>
    public static void Init(string message)
    {
        LogWithColor(ConsoleColor.Black, ConsoleColor.Green, "Init", ConsoleColor.Black, ConsoleColor.Gray, message);
    }

    /// <summary>Log a basic info message.</summary>
    public static void Info(string message)
    {
        LogWithColor(ConsoleColor.Black, ConsoleColor.Blue, "Info", ConsoleColor.Black, ConsoleColor.White, message);
    }

    /// <summary>Log a debug message, only in development mode.</summary>
    public static void Debug(string message)
    {
        if (WebServer.LogLevel <= LogLevel.Debug)
        {
            LogWithColor(ConsoleColor.Black, ConsoleColor.Gray, "Debug", ConsoleColor.Black, ConsoleColor.Gray, message);
        }
    }

    /// <summary>Log a warning message.</summary>
    public static void Warning(string message)
    {
        LogWithColor(ConsoleColor.Black, ConsoleColor.Yellow, "Warning", ConsoleColor.Black, ConsoleColor.Yellow, message);
    }

    /// <summary>Log an error message.</summary>
    public static void Error(string message)
    {
        LogWithColor(ConsoleColor.Black, ConsoleColor.Red, "Error", ConsoleColor.Black, ConsoleColor.Red, message);
    }

    /// <summary>Internal path to log a message with a given color under lock.</summary>
    public static void LogWithColor(ConsoleColor prefixBackground, ConsoleColor prefixForeground, string prefix, ConsoleColor messageBackground, ConsoleColor messageForeground, string message)
    {
        lock (ConsoleLock)
        {
            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"{DateTimeOffset.Now:HH:mm:ss.fff} [");
            Console.BackgroundColor = prefixBackground;
            Console.ForegroundColor = prefixForeground;
            Console.Write(prefix);
            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("] ");
            Console.BackgroundColor = messageBackground;
            Console.ForegroundColor = messageForeground;
            Console.WriteLine(message);
            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = ConsoleColor.White;
        }
    }
}
