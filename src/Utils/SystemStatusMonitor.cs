using Hardware.Info;
using System.Diagnostics;

namespace StableSwarmUI.Utils;

/// <summary>Utility to monitor system usage.</summary>
public static class SystemStatusMonitor
{
    /// <summary>The current process.</summary>
    public static Process SelfProc = Process.GetCurrentProcess();

    /// <summary>Last estimated process usage.</summary>
    public static double ProcessCPUUsage = 0;

    /// <summary>Tracker for CPU processor usage</summary>
    public static long LastProcessorTime = 0;

    /// <summary>Last time this monitor was ticked.</summary>
    public static long LastTick = Environment.TickCount64;

    /// <summary>General hardware info provider.</summary>
    public static HardwareInfo HardwareInfo = new();

    /// <summary>Updates system status.</summary>
    public static void Tick()
    {
        Task.Run(() =>
        {
            long newProcessorTime = SelfProc.TotalProcessorTime.Milliseconds;
            long newTick = Environment.TickCount64;
            ProcessCPUUsage = (newProcessorTime - LastProcessorTime) / (double)(newTick - LastTick);
            LastProcessorTime = newProcessorTime;
            LastTick = newTick;
            HardwareInfo.RefreshMemoryStatus();
        });
    }
}
