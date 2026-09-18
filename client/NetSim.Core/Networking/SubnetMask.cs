using System.Numerics;
using NetSim.Core.Common.Exceptions;

namespace NetSim.Core.Networking;

/// <summary>
/// An IPv4 subnet mask, as an immutable value object over a <see cref="uint"/> in host byte order.
/// Only a <em>contiguous</em> mask - a run of one bits followed by a run of zero bits - is a valid
/// value; a non-contiguous pattern such as <c>255.0.255.0</c> is rejected. Because a valid mask is
/// exactly equivalent to its CIDR prefix length, <see cref="FromPrefixLength"/> /
/// <see cref="PrefixLength"/> convert losslessly in both directions.
/// </summary>
public readonly record struct SubnetMask : IComparable<SubnetMask>
{
    private readonly uint _value;

    private SubnetMask(uint value) => _value = value;

    /// <summary>The mask as a 32-bit unsigned integer in host byte order.</summary>
    public uint Value => _value;

    /// <summary>255.255.255.255 - the /32 (single host) mask.</summary>
    public static SubnetMask Full { get; } = new(0xFFFF_FFFFu);

    /// <summary>0.0.0.0 - the /0 ("all addresses") mask. Also <c>default(SubnetMask)</c>.</summary>
    public static SubnetMask None { get; } = new(0u);

    /// <summary>
    /// Builds the mask for a CIDR prefix length. Throws <see cref="DomainException"/> when
    /// <paramref name="prefixLength"/> is outside 0-32.
    /// </summary>
    public static SubnetMask FromPrefixLength(int prefixLength)
    {
        if (prefixLength is < 0 or > 32)
        {
            throw new DomainException($"An IPv4 prefix length must be between 0 and 32, but was {prefixLength}.");
        }

        var value = prefixLength == 0 ? 0u : 0xFFFF_FFFFu << (32 - prefixLength);
        return new SubnetMask(value);
    }

    /// <summary>
    /// Parses a dotted-decimal subnet mask (e.g. <c>255.255.255.0</c>). Throws
    /// <see cref="DomainException"/> for a value that is not a valid IPv4 address <em>or</em> is a
    /// non-contiguous mask.
    /// </summary>
    public static SubnetMask Parse(string? text) =>
        TryParse(text, out var result)
            ? result
            : throw new DomainException($"'{text}' is not a valid, contiguous IPv4 subnet mask.");

    /// <summary>Non-throwing counterpart of <see cref="Parse"/>.</summary>
    public static bool TryParse(string? text, out SubnetMask result)
    {
        result = default;
        if (!IPv4Address.TryParse(text, out var address) || !IsContiguous(address.Value))
        {
            return false;
        }

        result = new SubnetMask(address.Value);
        return true;
    }

    /// <summary>True when <paramref name="value"/> is a run of one bits followed by a run of zero bits.</summary>
    public static bool IsContiguous(uint value)
    {
        // For "ones then zeros", the bitwise complement is "zeros then ones", i.e. (2^k - 1),
        // and (n & (n + 1)) == 0 holds exactly for numbers of that shape (including 0 and all-ones).
        var inverted = ~value;
        return (inverted & (inverted + 1)) == 0;
    }

    /// <summary>The equivalent CIDR prefix length (the number of leading one bits).</summary>
    public int PrefixLength => BitOperations.PopCount(_value);

    /// <summary>The inverse (wildcard / host) mask - <c>0.0.0.255</c> for <c>255.255.255.0</c>.</summary>
    public IPv4Address WildcardMask => new(~_value);

    /// <summary>The mask as an <see cref="IPv4Address"/> (for formatting / arithmetic).</summary>
    public IPv4Address AsAddress => new(_value);

    public int CompareTo(SubnetMask other) => _value.CompareTo(other._value);

    /// <summary>The canonical dotted-decimal form, e.g. <c>255.255.255.0</c>.</summary>
    public override string ToString() => AsAddress.ToString();
}
