using FreneticUtilities.FreneticToolkit;
using StableSwarmUI.Core;
using System.Diagnostics;

namespace StableSwarmUI.Utils;

/// <summary>Helpers for reading Nvidia GPU data.</summary>
public static class NvidiaUtil
{
    public record class NvidiaInfo(int ID, string GPUName, string DriverVersion, float Temperature, float UtilizationGPU, float UtilizationMemory, MemoryNum TotalMemory, MemoryNum FreeMemory, MemoryNum UsedMemory);

    /// <summary>Internal data for <see cref="NvidiaUtil"/>, do not access without a good reason.</summary>
    public static class Internal
    {
        public static bool HasNvidiaGPU = true;

        public static NvidiaInfo[] LastResultCache = null;

        public static long LastQueryTime;

        public static LockObject QueryLock = new();
    }

    /// <summary>Queries and returns data from nvidia-smi, or returns null if not possible (eg non-nvidia GPU).</summary>
    public static NvidiaInfo[] QueryNvidia()
    {
        float ParseFloat(string str) => float.TryParse(str.Replace("%", "").Trim().Replace(",", "."), out float f) ? f : 0;
        long ParseMemory(string m)
        {
            string[] parts = m.Split(' ', StringSplitOptions.TrimEntries);
            long v = long.TryParse(parts[0], out long lval) ? lval : 0;
            if (parts.Length == 1)
            {
                return v;
            }
            return parts[1] switch
            {
                "KiB" => v * 1024,
                "MiB" => v * 1024 * 1024,
                "GiB" => v * 1024 * 1024 * 1024,
                "TiB" => v * 1024 * 1024 * 1024 * 1024,
                _ => throw new Exception($"Unknown memory unit from nvidia-smi: {parts[1]}"),
            };
        }
        if (!Internal.HasNvidiaGPU)
        {
            return null;
        }
        try
        {
            lock (Internal.QueryLock)
            {
                if (Internal.LastResultCache is not null && Environment.TickCount64 < Internal.LastQueryTime + Program.ServerSettings.NvidiaQueryRateLimitMS)
                {
                      return Internal.LastResultCache;
                }
                ProcessStartInfo psi = new("nvidia-smi", "--query-gpu=gpu_name,driver_version,temperature.gpu,utilization.gpu,utilization.memory,memory.total,memory.free,memory.used --format=csv")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                Process p = Process.Start(psi);
                p.WaitForExit();
                p.StandardOutput.ReadLine(); // skip header
                List<NvidiaInfo> output = [];
                while (true)
                {
                    string data = p.StandardOutput.ReadLine();
                    if (string.IsNullOrWhiteSpace(data))
                    {
                        break;
                    }
                    string[] parts = data.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    string GPUName = parts[0];
                    string driverVersion = parts[1];
                    float temp = ParseFloat(parts[2]);
                    float utilGPU = ParseFloat(parts[3]);
                    float utilMemory = ParseFloat(parts[4]);
                    long totalMemory = ParseMemory(parts[5]);
                    long freeMemory = ParseMemory(parts[6]);
                    long usedMemory = ParseMemory(parts[7]);
                    NvidiaInfo info = new(output.Count, GPUName, driverVersion, temp, utilGPU, utilMemory, new(totalMemory), new(freeMemory), new(usedMemory));
                    output.Add(info);
                }
                Internal.LastResultCache = [.. output];
                Internal.LastQueryTime = Environment.TickCount64;
                return Internal.LastResultCache;
            }
        }
        catch (Exception)
        {
            Internal.HasNvidiaGPU = false;
        }
        return null;
    }
}
