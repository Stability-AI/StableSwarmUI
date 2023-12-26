namespace StableSwarmUI.Utils;

/// <summary>Mini-struct to hold data about a memory size number.</summary>
public struct MemoryNum
{
    public long InBytes;

    public MemoryNum(long inBytes)
    {
        InBytes = inBytes;
    }

    public readonly float KiB => InBytes / 1024f;

    public readonly float MiB => KiB / 1024f;

    public readonly float GiB => MiB / 1024f;

    public override readonly string ToString()
    {
        if (InBytes > 1024 * 1024 * 1024)
        {
            return $"{GiB:0.00} GiB";
        }
        else if (InBytes > 1024 * 1024)
        {
            return $"{MiB:0.00} MiB";
        }
        else if (InBytes > 1024)
        {
            return $"{KiB:0.00} KiB";
        }
        else
        {
            return $"{InBytes} B";
        }
    }
}
