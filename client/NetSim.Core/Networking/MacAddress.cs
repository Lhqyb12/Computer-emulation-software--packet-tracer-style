using NetSim.Core.Common.Exceptions;

namespace NetSim.Core.Networking;

/// <summary>
/// A 48-bit IEEE 802 link-layer (MAC) address, as an immutable value object. The canonical form
/// is six colon-separated upper-case hex octets (<c>00:1A:2B:3C:4D:5E</c>); parsing also accepts
/// hyphen-separated, Cisco dotted (<c>001A.2B3C.4D5E</c>) and bare 12-hex forms, but there is a
/// single internal representation so equality and hashing are format-independent.
///
/// Structural validation only: an address is either a well-formed 48-bit value or it is rejected -
/// there is no notion of a "semantically wrong" MAC here. Classification
/// (<see cref="Kind"/> / <see cref="IsBroadcast"/> / <see cref="IsMulticast"/> /
/// <see cref="IsUnicast"/> / <see cref="IsLocallyAdministered"/>) is derived from the address
/// bits and is all the Ethernet layer needs; multicast group membership and protocol behaviour
/// are out of scope.
/// </summary>
public readonly record struct MacAddress
{
    // Low 48 bits hold the address, first octet in the most-significant byte.
    private const ulong Mask48 = 0xFFFF_FFFF_FFFFUL;
    private const ulong BroadcastValue = Mask48;

    private readonly ulong _value;

    private MacAddress(ulong value)
    {
        if ((value & ~Mask48) != 0)
        {
            throw new DomainException($"MAC address value 0x{value:X} does not fit in 48 bits.");
        }

        _value = value;
    }

    /// <summary>The Ethernet broadcast address, FF:FF:FF:FF:FF:FF.</summary>
    public static MacAddress Broadcast { get; } = new(BroadcastValue);

    /// <summary>
    /// The all-zero address, 00:00:00:00:00:00. Used as the "unknown / not yet learned" target
    /// hardware address in an ARP request (Phase 20). Also <c>default(MacAddress)</c>.
    /// </summary>
    public static MacAddress Zero { get; } = new(0UL);

    /// <summary>The first octet - carries the I/G (multicast) bit (0x01) and the U/L (locally administered) bit (0x02).</summary>
    private byte FirstOctet => (byte)((_value >> 40) & 0xFF);

    /// <summary>True for FF:FF:FF:FF:FF:FF.</summary>
    public bool IsBroadcast => _value == BroadcastValue;

    /// <summary>True for the all-zero address 00:00:00:00:00:00 - an unset / "unknown" address (e.g. an ARP request's target hardware address).</summary>
    public bool IsUnspecified => _value == 0;

    /// <summary>True when the I/G bit is set but the address is not the broadcast address.</summary>
    public bool IsMulticast => !IsBroadcast && (FirstOctet & 0x01) != 0;

    /// <summary>True when the I/G bit is clear - the address identifies a single interface.</summary>
    public bool IsUnicast => (FirstOctet & 0x01) == 0;

    /// <summary>True when the U/L bit is set - the address was assigned locally, not by an OUI holder.</summary>
    public bool IsLocallyAdministered => (FirstOctet & 0x02) != 0;

    /// <summary>True when the U/L bit is clear - a (notionally) globally unique, vendor-assigned address.</summary>
    public bool IsUniversallyAdministered => !IsLocallyAdministered;

    /// <summary>Which of the three Ethernet delivery classes this address is - see <see cref="MacAddressKind"/>.</summary>
    public MacAddressKind Kind =>
        IsBroadcast ? MacAddressKind.Broadcast
        : (FirstOctet & 0x01) != 0 ? MacAddressKind.Multicast
        : MacAddressKind.Unicast;

    /// <summary>The six address octets, most-significant (first) octet at index 0.</summary>
    public byte[] GetBytes() =>
    [
        (byte)((_value >> 40) & 0xFF),
        (byte)((_value >> 32) & 0xFF),
        (byte)((_value >> 24) & 0xFF),
        (byte)((_value >> 16) & 0xFF),
        (byte)((_value >> 8) & 0xFF),
        (byte)(_value & 0xFF),
    ];

    /// <summary>
    /// Parses a MAC address in any supported form (colon, hyphen, Cisco dotted, or bare 12-hex).
    /// Throws <see cref="DomainException"/> for anything that is not exactly 48 bits of hex.
    /// </summary>
    public static MacAddress Parse(string? text) =>
        TryParse(text, out var result)
            ? result
            : throw new DomainException($"'{text}' is not a valid MAC address.");

    /// <summary>Non-throwing counterpart of <see cref="Parse"/>.</summary>
    public static bool TryParse(string? text, out MacAddress result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        ulong value = 0;
        var digits = 0;
        foreach (var ch in text)
        {
            if (ch is ':' or '-' or '.' or ' ')
            {
                continue;
            }

            if (digits >= 12 || !Uri.IsHexDigit(ch))
            {
                return false;
            }

            value = (value << 4) | (uint)Uri.FromHex(ch);
            digits++;
        }

        if (digits != 12)
        {
            return false;
        }

        result = new MacAddress(value);
        return true;
    }

    /// <summary>
    /// Creates a fresh random locally-administered unicast address: the I/G bit is cleared and the
    /// U/L bit is set, the other 46 bits are random. This guarantees the address can never collide
    /// with a real vendor-assigned NIC and never equals the host machine's own hardware address -
    /// the simulation stays entirely virtual (see docs/architecture/ethernet-layer.md).
    /// </summary>
    public static MacAddress CreateRandomUnicast()
    {
        Span<byte> bytes = stackalloc byte[6];
        Random.Shared.NextBytes(bytes);
        bytes[0] = (byte)((bytes[0] & 0xFC) | 0x02);

        ulong value = 0;
        foreach (var b in bytes)
        {
            value = (value << 8) | b;
        }

        return new MacAddress(value);
    }

    /// <summary>The canonical form: six upper-case colon-separated hex octets.</summary>
    public override string ToString() => string.Join(':', GetBytes().Select(b => b.ToString("X2")));
}
