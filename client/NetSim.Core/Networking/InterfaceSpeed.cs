using NetSim.Core.Common.Exceptions;

namespace NetSim.Core.Networking;

/// <summary>
/// The nominal link speed of a <see cref="NetworkInterface"/>, modelled as a comparable domain
/// value rather than a formatted string so later phases (packet timing, diagnostics, link
/// negotiation) can reason about it numerically. The canonical unit is bits per second; the
/// display string ("10 Mbps", "1 Gbps", ...) is derived, never the source of truth.
/// </summary>
public readonly struct InterfaceSpeed : IEquatable<InterfaceSpeed>, IComparable<InterfaceSpeed>
{
    private const long BitsPerKilobit = 1_000L;
    private const long BitsPerMegabit = 1_000_000L;
    private const long BitsPerGigabit = 1_000_000_000L;

    public InterfaceSpeed(long bitsPerSecond)
    {
        if (bitsPerSecond <= 0)
        {
            throw new DomainException("Interface speed must be a positive number of bits per second.");
        }

        BitsPerSecond = bitsPerSecond;
    }

    public long BitsPerSecond { get; }

    /// <summary>10 Mbps - legacy/base Ethernet.</summary>
    public static InterfaceSpeed Mbps10 => new(10 * BitsPerMegabit);

    /// <summary>100 Mbps - Fast Ethernet.</summary>
    public static InterfaceSpeed Mbps100 => new(100 * BitsPerMegabit);

    /// <summary>1 Gbps - Gigabit Ethernet.</summary>
    public static InterfaceSpeed Gbps1 => new(BitsPerGigabit);

    /// <summary>10 Gbps - Ten Gigabit Ethernet.</summary>
    public static InterfaceSpeed Gbps10 => new(10 * BitsPerGigabit);

    /// <summary>1.544 Mbps - a T1 serial line, a sensible default for a WAN serial interface.</summary>
    public static InterfaceSpeed SerialT1 => new(1_544_000L);

    /// <summary>9.6 Kbps - a console/management line.</summary>
    public static InterfaceSpeed Console9600 => new(9_600L);

    public static InterfaceSpeed FromMegabitsPerSecond(long megabitsPerSecond) => new(megabitsPerSecond * BitsPerMegabit);

    /// <summary>Human-readable form for the UI - "10 Mbps", "100 Mbps", "1 Gbps", "1.544 Mbps", "9.6 Kbps".</summary>
    public string ToDisplayString()
    {
        if (BitsPerSecond >= BitsPerGigabit)
        {
            return $"{Trim(BitsPerSecond / (double)BitsPerGigabit)} Gbps";
        }

        if (BitsPerSecond >= BitsPerMegabit)
        {
            return $"{Trim(BitsPerSecond / (double)BitsPerMegabit)} Mbps";
        }

        if (BitsPerSecond >= BitsPerKilobit)
        {
            return $"{Trim(BitsPerSecond / (double)BitsPerKilobit)} Kbps";
        }

        return $"{BitsPerSecond} bps";
    }

    private static string Trim(double value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    public bool Equals(InterfaceSpeed other) => BitsPerSecond == other.BitsPerSecond;

    public override bool Equals(object? obj) => obj is InterfaceSpeed other && Equals(other);

    public override int GetHashCode() => BitsPerSecond.GetHashCode();

    public int CompareTo(InterfaceSpeed other) => BitsPerSecond.CompareTo(other.BitsPerSecond);

    public override string ToString() => ToDisplayString();

    public static bool operator ==(InterfaceSpeed left, InterfaceSpeed right) => left.Equals(right);

    public static bool operator !=(InterfaceSpeed left, InterfaceSpeed right) => !left.Equals(right);

    public static bool operator <(InterfaceSpeed left, InterfaceSpeed right) => left.CompareTo(right) < 0;

    public static bool operator >(InterfaceSpeed left, InterfaceSpeed right) => left.CompareTo(right) > 0;

    public static bool operator <=(InterfaceSpeed left, InterfaceSpeed right) => left.CompareTo(right) <= 0;

    public static bool operator >=(InterfaceSpeed left, InterfaceSpeed right) => left.CompareTo(right) >= 0;
}
