using System.Diagnostics;

namespace StableSwarmUI.Utils;

public static class NvidiaUtil
{
    public record class NvidiaInfo(string GPUName, string DriverVersion, float Temperature, float UtilizationGPU, float UtilizationMemory, MemoryNum TotalMemory, MemoryNum FreeMemory, MemoryNum UsedMemory);

    private static bool HasNvidiaGPU = true;

    /// <summary>Queries and returns data from nvidia-smi, or returns null if not possible (eg non-nvidia GPU).</summary>
    public static NvidiaInfo QueryNvidia()
    {
        float ParseFloat(string f) => float.Parse(f.Replace("%", "").Trim().Replace(",", "."));
        long ParseMemory(string m)
        {
            string[] parts = m.Split(' ', StringSplitOptions.TrimEntries);
            long v = long.Parse(parts[0]);
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
        if (!HasNvidiaGPU)
        {
            return null;
        }
        try
        {
            ProcessStartInfo psi = new("nvidia-smi", "--query-gpu=gpu_name,driver_version,temperature.gpu,utilization.gpu,utilization.memory,memory.total,memory.free,memory.used --format=csv")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            Process p = Process.Start(psi);
            p.WaitForExit();
            p.StandardOutput.ReadLine(); // Header
            string data = p.StandardOutput.ReadLine();
            string[] parts = data.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            string GPUName = parts[0];
            string driverVersion = parts[1];
            float temp = ParseFloat(parts[2]);
            float utilGPU = ParseFloat(parts[3]);
            float utilMemory = ParseFloat(parts[4]);
            long totalMemory = ParseMemory(parts[5]);
            long freeMemory = ParseMemory(parts[6]);
            long usedMemory = ParseMemory(parts[7]);
            NvidiaInfo info = new(GPUName, driverVersion, temp, utilGPU, utilMemory, new(totalMemory), new(freeMemory), new(usedMemory));
            Logs.Verbose($"nvidia-smi: {data}: {info}");
            return info;
        }
        catch (Exception)
        {
            HasNvidiaGPU = false;
        }
        return null;
    }
}
