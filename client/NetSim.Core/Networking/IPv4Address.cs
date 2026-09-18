using System.Globalization;
using NetSim.Core.Common.Exceptions;

namespace NetSim.Core.Networking;

/// <summary>
/// A 32-bit IPv4 address, as an immutable value object. The single internal representation is a
/// <see cref="uint"/> in host byte order (the first dotted octet is the most-significant byte), so
/// value-based equality, hashing and ordering come for free and are independent of how the address
/// was written. The canonical text form is dotted-decimal (<c>192.168.1.10</c>).
///
/// This is the one place IPv4 addresses are parsed and validated - the rest of the domain passes
/// <see cref="IPv4Address"/> values around, never raw strings. Validation is <em>structural</em>
/// (is this four octets of 0-255) and is kept separate from <em>classification</em>
/// (<see cref="Category"/> / <see cref="IsPrivate"/> / <see cref="IsLoopback"/> / ...), which is
/// derived from the bits and drives no behaviour on its own.
/// </summary>
public readonly record struct IPv4Address : IComparable<IPv4Address>
{
    private readonly uint _value;

    /// <summary>Creates an address from its 32-bit value in host byte order (first octet = most-significant byte).</summary>
    public IPv4Address(uint value) => _value = value;

    /// <summary>The address as a 32-bit unsigned integer in host byte order.</summary>
    public uint Value => _value;

    /// <summary>0.0.0.0 - the unspecified / "this host" address. Also <c>default(IPv4Address)</c>.</summary>
    public static IPv4Address Any { get; } = new(0u);

    /// <summary>127.0.0.1 - the canonical loopback address.</summary>
    public static IPv4Address Loopback { get; } = new(0x7F00_0001u);

    /// <summary>255.255.255.255 - the limited broadcast address.</summary>
    public static IPv4Address Broadcast { get; } = new(0xFFFF_FFFFu);

    private byte Octet1 => (byte)((_value >> 24) & 0xFF);

    private byte Octet2 => (byte)((_value >> 16) & 0xFF);

    private byte Octet3 => (byte)((_value >> 8) & 0xFF);

    private byte Octet4 => (byte)(_value & 0xFF);

    /// <summary>
    /// Parses a dotted-decimal IPv4 address. Throws <see cref="DomainException"/> for anything that
    /// is not exactly four decimal octets in the range 0-255 (e.g. <c>256.1.1.1</c>, <c>192.168.1</c>,
    /// <c>192.168.1.1.1</c>, <c>abc.def.ghi.jkl</c>, an empty string, a negative octet).
    /// </summary>
    public static IPv4Address Parse(string? text) =>
        TryParse(text, out var result)
            ? result
            : throw new DomainException($"'{text}' is not a valid IPv4 address.");

    /// <summary>Non-throwing counterpart of <see cref="Parse"/>.</summary>
    public static bool TryParse(string? text, out IPv4Address result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parts = text.Split('.');
        if (parts.Length != 4)
        {
            return false;
        }

        uint value = 0;
        foreach (var part in parts)
        {
            // NumberStyles.None rejects leading/trailing whitespace and any sign, so a negative
            // octet or "192. 168.1.1" never parses.
            if (!byte.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var octet))
            {
                return false;
            }

            value = (value << 8) | octet;
        }

        result = new IPv4Address(value);
        return true;
    }

    /// <summary>Builds an address from four octets, most-significant (first dotted) octet at index 0.</summary>
    public static IPv4Address FromBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 4)
        {
            throw new DomainException($"An IPv4 address needs exactly 4 bytes, but {bytes.Length} were supplied.");
        }

        return new IPv4Address(((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3]);
    }

    /// <summary>The four address octets, most-significant (first dotted) octet at index 0.</summary>
    public byte[] GetBytes() => [Octet1, Octet2, Octet3, Octet4];

    // ---- Classification (derived from the bits; no behaviour) ----

    /// <summary>True for 0.0.0.0.</summary>
    public bool IsUnspecified => _value == 0;

    /// <summary>True for 127.0.0.0/8.</summary>
    public bool IsLoopback => (_value & 0xFF00_0000u) == 0x7F00_0000u;

    /// <summary>True for exactly 255.255.255.255.</summary>
    public bool IsLimitedBroadcast => _value == 0xFFFF_FFFFu;

    /// <summary>True for 169.254.0.0/16 (RFC 3927 link-local auto-configuration).</summary>
    public bool IsLinkLocal => (_value & 0xFFFF_0000u) == 0xA9FE_0000u;

    /// <summary>True for 224.0.0.0/4 (class-D multicast).</summary>
    public bool IsMulticast => (_value & 0xF000_0000u) == 0xE000_0000u;

    /// <summary>True for an RFC 1918 private range: 10.0.0.0/8, 172.16.0.0/12 or 192.168.0.0/16.</summary>
    public bool IsPrivate =>
        (_value & 0xFF00_0000u) == 0x0A00_0000u
        || (_value & 0xFFF0_0000u) == 0xAC10_0000u
        || (_value & 0xFFFF_0000u) == 0xC0A8_0000u;

    /// <summary>True for a globally-routable unicast address - none of the special classifications above.</summary>
    public bool IsPublic =>
        !IsUnspecified && !IsLoopback && !IsLimitedBroadcast && !IsLinkLocal && !IsMulticast && !IsPrivate;

    /// <summary>The single mutually-exclusive <see cref="IPv4AddressCategory"/> this address falls into.</summary>
    public IPv4AddressCategory Category =>
        IsUnspecified ? IPv4AddressCategory.Unspecified
        : IsLimitedBroadcast ? IPv4AddressCategory.LimitedBroadcast
        : IsLoopback ? IPv4AddressCategory.Loopback
        : IsLinkLocal ? IPv4AddressCategory.LinkLocal
        : IsPrivate ? IPv4AddressCategory.Private
        : IsMulticast ? IPv4AddressCategory.Multicast
        : IPv4AddressCategory.Public;

    public int CompareTo(IPv4Address other) => _value.CompareTo(other._value);

    public static bool operator <(IPv4Address left, IPv4Address right) => left._value < right._value;

    public static bool operator >(IPv4Address left, IPv4Address right) => left._value > right._value;

    public static bool operator <=(IPv4Address left, IPv4Address right) => left._value <= right._value;

    public static bool operator >=(IPv4Address left, IPv4Address right) => left._value >= right._value;

    /// <summary>The canonical dotted-decimal form, e.g. <c>192.168.1.10</c>.</summary>
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Octet1}.{Octet2}.{Octet3}.{Octet4}");
}
