using FreneticUtilities.FreneticToolkit;

namespace StableSwarmUI.Utils;

/// <summary>Central internal logging handler.</summary>
public static class Logs
{
    /// <summary>Thread lock to prevent messages overlapping.</summary>
    public static LockObject ConsoleLock = new();

    public enum LogLevel: int
    {
        Verbose, Debug, Info, Init, Warning, Error, None
    }

    /// <summary>Minimum primary logger log level.</summary>
    public static LogLevel MinimumLevel = LogLevel.Info;

    /// <summary>Log a verbose debug message, only in development mode.</summary>
    public static void Verbose(string message)
    {
        LogWithColor(ConsoleColor.Black, ConsoleColor.Gray, "Verbose", ConsoleColor.Black, ConsoleColor.Gray, message, LogLevel.Verbose);
    }

    /// <summary>Log a debug message, only in development mode.</summary>
    public static void Debug(string message)
    {
        LogWithColor(ConsoleColor.Black, ConsoleColor.Gray, "Debug", ConsoleColor.Black, ConsoleColor.Gray, message, LogLevel.Debug);
    }

    /// <summary>Log a basic info message.</summary>
    public static void Info(string message)
    {
        LogWithColor(ConsoleColor.Black, ConsoleColor.Cyan, "Info", ConsoleColor.Black, ConsoleColor.White, message, LogLevel.Info);
    }

    /// <summary>Log an initialization-related message.</summary>
    public static void Init(string message)
    {
        LogWithColor(ConsoleColor.Black, ConsoleColor.Green, "Init", ConsoleColor.Black, ConsoleColor.Gray, message, LogLevel.Init);
    }

    /// <summary>Log a warning message.</summary>
    public static void Warning(string message)
    {
        LogWithColor(ConsoleColor.Black, ConsoleColor.Yellow, "Warning", ConsoleColor.Black, ConsoleColor.Yellow, message, LogLevel.Warning);
    }

    /// <summary>Log an error message.</summary>
    public static void Error(string message)
    {
        LogWithColor(ConsoleColor.Black, ConsoleColor.Red, "Error", ConsoleColor.Black, ConsoleColor.Red, message, LogLevel.Error);
    }

    /// <summary>Helper to track recent log outputs.</summary>
    public class LogTracker
    {
        /// <summary>How many log messages to keep at a time.</summary>
        public static int MaxTracked = 512;

        /// <summary>A queue of all recent log messages.</summary>
        public Queue<LogMessage> Messages = new(MaxTracked);

        /// <summary>Track a new log message.</summary>
        public void Track(string message)
        {
            Messages.Enqueue(new LogMessage(DateTimeOffset.Now, message));
            if (Messages.Count > MaxTracked)
            {
                Messages.Dequeue();
            }
        }
    }

    /// <summary>Tiny struct representing a logged message, with a timestamp and its message content.</summary>
    public record struct LogMessage(DateTimeOffset Time, string Message);

    /// <summary>All current log trackers.</summary>
    public static LogTracker[] Trackers = new LogTracker[(int)LogLevel.None];

    /// <summary>Named set of other log trackers (eg backends).</summary>
    public static ConcurrentDictionary<string, LogTracker> OtherTrackers = new();

    static Logs()
    {
        for (int i = 0; i < Trackers.Length; i++)
        {
            Trackers[i] = new();
        }
    }

    /// <summary>Internal path to log a message with a given color under lock.</summary>
    public static void LogWithColor(ConsoleColor prefixBackground, ConsoleColor prefixForeground, string prefix, ConsoleColor messageBackground, ConsoleColor messageForeground, string message, LogLevel level)
    {
        lock (ConsoleLock)
        {
            Trackers[(int)level].Track(message);
            if (MinimumLevel > level)
            {
                return;
            }
            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write($"{DateTimeOffset.Now:HH:mm:ss.fff} [");
            Console.BackgroundColor = prefixBackground;
            Console.ForegroundColor = prefixForeground;
            Console.Write(prefix);
            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write("] ");
            Console.BackgroundColor = messageBackground;
            Console.ForegroundColor = messageForeground;
            Console.WriteLine(message);
            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = ConsoleColor.White;
        }
    }
}
