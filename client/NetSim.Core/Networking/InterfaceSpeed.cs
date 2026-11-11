using NetSim.Core.Common.Exceptions;   // DomainException

namespace NetSim.Core.Networking;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The speed of a port, kept as a number (bits per second) and not as text,
// so later code can compare speeds ("which is faster?") and calculate with them.
// The text "100 Mbps" is only made from the number when it is shown on the screen.
// "readonly struct" = a small value that can't change after it is made, copied like a number.
// IEquatable = can check "are two speeds equal"; IComparable = can check "which one is bigger"
public readonly struct InterfaceSpeed : IEquatable<InterfaceSpeed>, IComparable<InterfaceSpeed>
{
    // "const" = a fixed number that never changes. "_" inside a number is only for reading, like a comma
    private const long BitsPerKilobit = 1_000L;            // 1 Kbps = one thousand bits per second
    private const long BitsPerMegabit = 1_000_000L;        // 1 Mbps = one million bits per second
    private const long BitsPerGigabit = 1_000_000_000L;    // 1 Gbps = one billion bits per second

    // Purpose: Builds a speed from a number of bits per second.
    // Input:   bitsPerSecond - the speed; must be more than 0.
    // Output:  none (a constructor). Throws DomainException for 0 or a negative number.
    public InterfaceSpeed(long bitsPerSecond)
    {
        // A speed of 0 or less makes no sense
        if (bitsPerSecond <= 0)
        {
            throw new DomainException("Interface speed must be a positive number of bits per second.");
        }

        // Keep the number
        BitsPerSecond = bitsPerSecond;
    }

    // The speed itself, in bits per second
    public long BitsPerSecond { get; }

    // Ready-made common speeds, so other code writes InterfaceSpeed.Mbps100 instead of a big number.
    // "=>" = every read builds the value from the formula on the right
    public static InterfaceSpeed Mbps10 => new(10 * BitsPerMegabit);      // 10 Mbps - old Ethernet
    public static InterfaceSpeed Mbps100 => new(100 * BitsPerMegabit);    // 100 Mbps - Fast Ethernet
    public static InterfaceSpeed Gbps1 => new(BitsPerGigabit);            // 1 Gbps - Gigabit Ethernet
    public static InterfaceSpeed Gbps10 => new(10 * BitsPerGigabit);      // 10 Gbps - Ten Gigabit
    public static InterfaceSpeed SerialT1 => new(1_544_000L);             // 1.544 Mbps - a T1 serial line between routers
    public static InterfaceSpeed Console9600 => new(9_600L);              // 9.6 Kbps - a console cable

    // Purpose: Builds a speed from a number of megabits per second.
    // Input:   megabitsPerSecond - e.g. 100.
    // Output:  the speed (e.g. 100 -> 100,000,000 bits per second).
    public static InterfaceSpeed FromMegabitsPerSecond(long megabitsPerSecond) => new(megabitsPerSecond * BitsPerMegabit);

    // Purpose: Makes the text shown on the screen.
    // Input:   nothing.
    // Output:  e.g. "10 Mbps", "1 Gbps", "1.544 Mbps", "9.6 Kbps".
    public string ToDisplayString()
    {
        // A billion or more - show in Gbps. "(double)" = divide with a decimal point, not a whole number
        if (BitsPerSecond >= BitsPerGigabit)
        {
            return $"{Trim(BitsPerSecond / (double)BitsPerGigabit)} Gbps";
        }

        // A million or more - show in Mbps
        if (BitsPerSecond >= BitsPerMegabit)
        {
            return $"{Trim(BitsPerSecond / (double)BitsPerMegabit)} Mbps";
        }

        // A thousand or more - show in Kbps
        if (BitsPerSecond >= BitsPerKilobit)
        {
            return $"{Trim(BitsPerSecond / (double)BitsPerKilobit)} Kbps";
        }

        // Less than a thousand - show the plain number
        return $"{BitsPerSecond} bps";
    }

    // Purpose: Writes a number with at most 3 digits after the point, and none when not needed.
    // Input:   value - e.g. 1.544 or 100.0.
    // Output:  e.g. "1.544" or "100". InvariantCulture = always a "." as the point, whatever the PC's language
    private static string Trim(double value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    // Purpose: Checks whether two speeds are the same.
    // Input:   other - the other speed.
    // Output:  true when both have the same number of bits per second.
    public bool Equals(InterfaceSpeed other) => BitsPerSecond == other.BitsPerSecond;

    // Purpose: The same check, for when the other value arrives as a general "object".
    // Input:   obj - any value.
    // Output:  true only when it is an InterfaceSpeed with the same number. "is ... other" = check the type and name it
    public override bool Equals(object? obj) => obj is InterfaceSpeed other && Equals(other);

    // Purpose: A number used by lists and dictionaries to find values fast; equal speeds give the same number.
    // Input:   nothing.
    // Output:  the hash of the bits-per-second number.
    public override int GetHashCode() => BitsPerSecond.GetHashCode();

    // Purpose: Compares two speeds.
    // Input:   other - the other speed.
    // Output:  less than 0 = this one is slower, 0 = equal, more than 0 = this one is faster.
    public int CompareTo(InterfaceSpeed other) => BitsPerSecond.CompareTo(other.BitsPerSecond);

    // Purpose: The text of the speed, e.g. when it is put inside a string.
    // Input:   nothing.
    // Output:  the same as ToDisplayString().
    public override string ToString() => ToDisplayString();

    // Operators, so code can write "a == b" or "a > b" with two speeds. Each one uses Equals / CompareTo above
    public static bool operator ==(InterfaceSpeed left, InterfaceSpeed right) => left.Equals(right);              // equal
    public static bool operator !=(InterfaceSpeed left, InterfaceSpeed right) => !left.Equals(right);             // not equal
    public static bool operator <(InterfaceSpeed left, InterfaceSpeed right) => left.CompareTo(right) < 0;        // slower
    public static bool operator >(InterfaceSpeed left, InterfaceSpeed right) => left.CompareTo(right) > 0;        // faster
    public static bool operator <=(InterfaceSpeed left, InterfaceSpeed right) => left.CompareTo(right) <= 0;      // slower or equal
    public static bool operator >=(InterfaceSpeed left, InterfaceSpeed right) => left.CompareTo(right) >= 0;      // faster or equal
}
