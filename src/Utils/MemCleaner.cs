using StableSwarmUI.Core;
using StableSwarmUI.WebAPI;

namespace StableSwarmUI.Utils;

/// <summary>Simple utility class that keeps memory cleared up automatically over time based on user settings.</summary>
public static class MemCleaner
{
    public static long TimeSinceLastGen = 0;

    public static bool HasClearedVRAM = false, HasClearedSysRAM = false;

    public static void TickIsGenerating()
    {
        TimeSinceLastGen = 0;
        HasClearedVRAM = false;
        HasClearedSysRAM = false;
    }

    public static void TickNoGenerations()
    {
        if (TimeSinceLastGen == 0)
        {
            TimeSinceLastGen = Environment.TickCount64;
        }
        else if (Environment.TickCount64 - TimeSinceLastGen > Program.ServerSettings.Backends.ClearVRAMAfterMinutes * 60 * 1000 && !HasClearedVRAM)
        {
            BackendAPI.FreeBackendMemory(null, false).Wait();
            HasClearedVRAM = true;
        }
        else if (Environment.TickCount64 - TimeSinceLastGen > Program.ServerSettings.Backends.ClearSystemRAMAfterMinutes * 60 * 1000 && !HasClearedSysRAM)
        {
            BackendAPI.FreeBackendMemory(null, false).Wait();
            HasClearedSysRAM = true;
        }
    }
}
