using System.Buffers.Binary;
using System.Globalization;
using NetSim.Core.Common.Exceptions;

namespace NetSim.Core.Networking;

/// <summary>
/// A 128-bit IPv6 address, as an immutable value object. The single internal representation is a
/// <see cref="UInt128"/> in host byte order (the first written group is the most-significant 16
/// bits), so value-based equality, hashing and ordering come for free and are independent of how
/// the address was written. The canonical text form follows RFC 5952 (lower-case hex, leading
/// zeros stripped, the longest run of zero groups compressed to <c>::</c>) - e.g.
/// <c>2001:0db8:0000:0000:0000:0000:0000:0001</c> and <c>2001:db8::1</c> parse to the same value
/// and both render as <c>2001:db8::1</c>.
///
/// This is the one place IPv6 addresses are parsed and validated - the rest of the domain passes
/// <see cref="IPv6Address"/> values around, never raw strings. Validation is <em>structural</em>
/// (is this a well-formed 128-bit address) and is kept separate from <em>classification</em>
/// (<see cref="Category"/> / <see cref="IsLinkLocal"/> / <see cref="IsMulticast"/> / ...), which is
/// derived from the bits and drives no behaviour on its own.
/// </summary>
public readonly record struct IPv6Address : IComparable<IPv6Address>
{
    /// <summary>The number of bytes in an IPv6 address.</summary>
    public const int ByteLength = 16;

    private readonly UInt128 _value;

    /// <summary>Creates an address from its 128-bit value in host byte order (first group = most-significant 16 bits).</summary>
    public IPv6Address(UInt128 value) => _value = value;

    /// <summary>The address as a 128-bit unsigned integer in host byte order.</summary>
    public UInt128 Value => _value;

    /// <summary><c>::</c> - the unspecified address. Also <c>default(IPv6Address)</c>.</summary>
    public static IPv6Address Unspecified { get; } = new(UInt128.Zero);

    /// <summary><c>::1</c> - the loopback address.</summary>
    public static IPv6Address Loopback { get; } = new(UInt128.One);

    private byte Byte0 => (byte)(_value >> 120);

    private byte Byte1 => (byte)(_value >> 112);

    /// <summary>
    /// Parses an IPv6 address in any standard textual form (full, zero-compressed, loopback,
    /// unspecified, link-local, an embedded IPv4 tail). Throws <see cref="DomainException"/> for
    /// anything malformed - more than one <c>::</c>, too many or too few groups, a group with more
    /// than four hex digits, a non-hex character, a zone index, surrounding brackets, whitespace,
    /// an empty string.
    /// </summary>
    public static IPv6Address Parse(string? text) =>
        TryParse(text, out var result)
            ? result
            : throw new DomainException($"'{text}' is not a valid IPv6 address.");

    /// <summary>Non-throwing counterpart of <see cref="Parse"/>.</summary>
    public static bool TryParse(string? text, out IPv6Address result)
    {
        result = default;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch))
            {
                return false;
            }
        }

        // No zone index (fe80::1%eth0) and no bracketed form ([::1]) - those are transport/URI
        // concerns, not the address itself.
        if (text.IndexOf('%') >= 0 || text.IndexOf('[') >= 0 || text.IndexOf(']') >= 0)
        {
            return false;
        }

        var doubleColon = text.IndexOf("::", StringComparison.Ordinal);
        string head, tail;
        bool compressed;
        if (doubleColon >= 0)
        {
            if (text.IndexOf("::", doubleColon + 1, StringComparison.Ordinal) >= 0)
            {
                return false; // more than one "::"
            }

            compressed = true;
            head = text[..doubleColon];
            tail = text[(doubleColon + 2)..];
        }
        else
        {
            compressed = false;
            head = text;
            tail = string.Empty;
        }

        // A stray single ':' at an edge (not part of the one allowed "::") is malformed.
        if (head.EndsWith(':') || tail.StartsWith(':') || (!compressed && head.StartsWith(':')))
        {
            return false;
        }

        var groups = new List<ushort>(8);

        if (head.Length > 0 && !TryParseGroupList(head, allowTrailingIPv4: !compressed, groups))
        {
            return false;
        }

        var headCount = groups.Count;

        if (tail.Length > 0 && !TryParseGroupList(tail, allowTrailingIPv4: true, groups))
        {
            return false;
        }

        var tailCount = groups.Count - headCount;

        if (compressed)
        {
            // "::" has to stand in for at least one all-zero group.
            if (groups.Count > 7)
            {
                return false;
            }
        }
        else if (groups.Count != 8)
        {
            return false;
        }

        Span<byte> bytes = stackalloc byte[ByteLength];
        bytes.Clear();

        for (var i = 0; i < headCount; i++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(bytes[(i * 2)..], groups[i]);
        }

        for (var i = 0; i < tailCount; i++)
        {
            var pos = ByteLength - ((tailCount - i) * 2);
            BinaryPrimitives.WriteUInt16BigEndian(bytes[pos..], groups[headCount + i]);
        }

        result = new IPv6Address(BinaryPrimitives.ReadUInt128BigEndian(bytes));
        return true;
    }

    /// <summary>Builds an address from its 16 bytes, most-significant byte first.</summary>
    public static IPv6Address FromBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != ByteLength)
        {
            throw new DomainException($"An IPv6 address needs exactly {ByteLength} bytes, but {bytes.Length} were supplied.");
        }

        return new IPv6Address(BinaryPrimitives.ReadUInt128BigEndian(bytes));
    }

    /// <summary>The 16 address bytes, most-significant byte first.</summary>
    public byte[] GetBytes()
    {
        var bytes = new byte[ByteLength];
        BinaryPrimitives.WriteUInt128BigEndian(bytes, _value);
        return bytes;
    }

    // ---- Classification (derived from the bits; no behaviour) ----

    /// <summary>True for <c>::</c>.</summary>
    public bool IsUnspecified => _value == UInt128.Zero;

    /// <summary>True for <c>::1</c>.</summary>
    public bool IsLoopback => _value == UInt128.One;

    /// <summary>True for <c>ff00::/8</c>.</summary>
    public bool IsMulticast => Byte0 == 0xFF;

    /// <summary>True for <c>fe80::/10</c> (link-local unicast).</summary>
    public bool IsLinkLocal => Byte0 == 0xFE && (Byte1 & 0xC0) == 0x80;

    /// <summary>True for <c>fc00::/7</c> (unique local addresses, RFC 4193).</summary>
    public bool IsUniqueLocal => (Byte0 & 0xFE) == 0xFC;

    /// <summary>True for <c>2000::/3</c> - the currently-allocated global unicast space.</summary>
    public bool IsGlobalUnicast => (Byte0 & 0xE0) == 0x20;

    /// <summary>The single mutually-exclusive <see cref="IPv6AddressCategory"/> this address falls into.</summary>
    public IPv6AddressCategory Category =>
        IsUnspecified ? IPv6AddressCategory.Unspecified
        : IsLoopback ? IPv6AddressCategory.Loopback
        : IsMulticast ? IPv6AddressCategory.Multicast
        : IsLinkLocal ? IPv6AddressCategory.LinkLocal
        : IsUniqueLocal ? IPv6AddressCategory.UniqueLocal
        : IsGlobalUnicast ? IPv6AddressCategory.GlobalUnicast
        : IPv6AddressCategory.Other;

    public int CompareTo(IPv6Address other) => _value.CompareTo(other._value);

    public static bool operator <(IPv6Address left, IPv6Address right) => left._value < right._value;

    public static bool operator >(IPv6Address left, IPv6Address right) => left._value > right._value;

    public static bool operator <=(IPv6Address left, IPv6Address right) => left._value <= right._value;

    public static bool operator >=(IPv6Address left, IPv6Address right) => left._value >= right._value;

    /// <summary>The canonical RFC 5952 form, e.g. <c>2001:db8::1</c>.</summary>
    public override string ToString()
    {
        Span<byte> bytes = stackalloc byte[ByteLength];
        BinaryPrimitives.WriteUInt128BigEndian(bytes, _value);

        Span<ushort> groups = stackalloc ushort[8];
        for (var i = 0; i < 8; i++)
        {
            groups[i] = BinaryPrimitives.ReadUInt16BigEndian(bytes[(i * 2)..]);
        }

        // Find the longest run of consecutive zero groups; the leftmost wins a tie (RFC 5952).
        int bestStart = -1, bestLength = 0, runStart = -1, runLength = 0;
        for (var i = 0; i < 8; i++)
        {
            if (groups[i] == 0)
            {
                if (runStart < 0)
                {
                    runStart = i;
                    runLength = 0;
                }

                runLength++;
                if (runLength > bestLength)
                {
                    bestLength = runLength;
                    bestStart = runStart;
                }
            }
            else
            {
                runStart = -1;
                runLength = 0;
            }
        }

        // A single zero group is not compressed.
        if (bestLength < 2)
        {
            var all = new string[8];
            for (var i = 0; i < 8; i++)
            {
                all[i] = groups[i].ToString("x", CultureInfo.InvariantCulture);
            }

            return string.Join(':', all);
        }

        var headParts = new string[bestStart];
        for (var i = 0; i < bestStart; i++)
        {
            headParts[i] = groups[i].ToString("x", CultureInfo.InvariantCulture);
        }

        var tailFrom = bestStart + bestLength;
        var tailParts = new string[8 - tailFrom];
        for (var i = tailFrom; i < 8; i++)
        {
            tailParts[i - tailFrom] = groups[i].ToString("x", CultureInfo.InvariantCulture);
        }

        return string.Join(':', headParts) + "::" + string.Join(':', tailParts);
    }

    private static bool TryParseGroupList(string segment, bool allowTrailingIPv4, List<ushort> into)
    {
        var tokens = segment.Split(':');
        for (var t = 0; t < tokens.Length; t++)
        {
            var token = tokens[t];
            if (token.Length == 0)
            {
                return false; // an empty group means ':::' or a doubled ':' beyond the one allowed
            }

            var isLast = t == tokens.Length - 1;

            if (token.IndexOf('.') >= 0)
            {
                if (!isLast || !allowTrailingIPv4 || !IPv4Address.TryParse(token, out var v4) || into.Count + 2 > 8)
                {
                    return false;
                }

                into.Add((ushort)(v4.Value >> 16));
                into.Add((ushort)(v4.Value & 0xFFFF));
                continue;
            }

            if (token.Length > 4)
            {
                return false;
            }

            ushort value = 0;
            foreach (var ch in token)
            {
                var digit = HexValue(ch);
                if (digit < 0)
                {
                    return false;
                }

                value = (ushort)((value << 4) | digit);
            }

            if (into.Count + 1 > 8)
            {
                return false;
            }

            into.Add(value);
        }

        return true;
    }

    private static int HexValue(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'f' => c - 'a' + 10,
        >= 'A' and <= 'F' => c - 'A' + 10,
        _ => -1,
    };
}
