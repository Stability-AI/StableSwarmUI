namespace StableSwarmUI.Utils;

/// <summary>Mini-struct to hold data about a memory (bytes) size number.</summary>
public struct MemoryNum(long inBytes)
{
    public long InBytes = inBytes;

    public readonly float KiB => InBytes / 1024f;

    public readonly float MiB => (float)(InBytes / (1024.0 * 1024));

    public readonly float GiB => (float)(InBytes / (1024.0 * 1024 * 1024));

    public readonly float TiB => GiB / 1024f;

    public override readonly string ToString()
    {
        if (InBytes > 1024L * 1024 * 1024 * 1024)
        {
            return $"{TiB:0.00} TiB";
        }
        else if (InBytes > 1024L * 1024 * 1024)
        {
            return $"{GiB:0.00} GiB";
        }
        else if (InBytes > 1024L * 1024)
        {
            return $"{MiB:0.00} MiB";
        }
        else if (InBytes > 1024L)
        {
            return $"{KiB:0.00} KiB";
        }
        else
        {
            return $"{InBytes} B";
        }
    }
}
