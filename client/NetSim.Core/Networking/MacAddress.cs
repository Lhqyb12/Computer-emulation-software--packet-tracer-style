using NetSim.Core.Common.Exceptions;   // DomainException

namespace NetSim.Core.Networking;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// A MAC address - the physical address of a network port: 48 bits, written as 6 bytes in hex,
// e.g. 00:1A:2B:3C:4D:5E. Every port in the simulator gets one when it is created.
// Inside, the whole address is kept as ONE number, so two addresses written differently
// ("00-1A-..." and "001A.2B3C...") are still equal.
// "readonly record struct" = can't change after it is made, compared by value, copied like a number
public readonly record struct MacAddress
{
    // 48 bits all set to 1 = 0xFFFFFFFFFFFF. "0x" = a number written in hex; "UL" = an unsigned 64-bit number
    private const ulong Mask48 = 0xFFFF_FFFF_FFFFUL;

    // The broadcast address FF:FF:FF:FF:FF:FF is exactly all 48 bits set
    private const ulong BroadcastValue = Mask48;

    // The address itself. "ulong" = a 64-bit number; only the lowest 48 bits are used, the first byte is the highest
    private readonly ulong _value;

    // Purpose: Builds an address from a number. Private: outside code uses Parse / CreateRandomUnicast instead.
    // Input:   value - the 48-bit number.
    // Output:  none (a constructor). Throws DomainException when the number is bigger than 48 bits.
    private MacAddress(ulong value)
    {
        // "~Mask48" = every bit ABOVE the 48; "&" keeps only those. Not 0 = the number is too big
        if ((value & ~Mask48) != 0)
        {
            throw new DomainException($"MAC address value 0x{value:X} does not fit in 48 bits.");
        }

        // Keep the number
        _value = value;
    }

    // The broadcast address FF:FF:FF:FF:FF:FF - "send to everyone". "{ get; } = ..." = made once and kept
    public static MacAddress Broadcast { get; } = new(BroadcastValue);

    // The empty address 00:00:00:00:00:00 - "not known yet" (used later by ARP, in Part 4)
    public static MacAddress Zero { get; } = new(0UL);

    // The first byte of the address. ">> 40" = move it down 40 bits to the bottom; "& 0xFF" = keep only 8 bits
    private byte FirstOctet => (byte)((_value >> 40) & 0xFF);

    // True for FF:FF:FF:FF:FF:FF
    public bool IsBroadcast => _value == BroadcastValue;

    // True for 00:00:00:00:00:00
    public bool IsUnspecified => _value == 0;

    // A group address: the lowest bit of the first byte is 1, and it is not the broadcast address
    public bool IsMulticast => !IsBroadcast && (FirstOctet & 0x01) != 0;

    // A single-port address: the lowest bit of the first byte is 0
    public bool IsUnicast => (FirstOctet & 0x01) == 0;

    // The second-lowest bit of the first byte is 1 = the address was made up locally (like ours), not by a manufacturer
    public bool IsLocallyAdministered => (FirstOctet & 0x02) != 0;

    // The opposite - an address a manufacturer gave
    public bool IsUniversallyAdministered => !IsLocallyAdministered;

    // Which of the three kinds this address is. "a ? b : c" = if a then b, otherwise c
    public MacAddressKind Kind =>
        IsBroadcast ? MacAddressKind.Broadcast                     // all bits set - broadcast
        : (FirstOctet & 0x01) != 0 ? MacAddressKind.Multicast      // group bit set - multicast
        : MacAddressKind.Unicast;                                  // otherwise - one port

    // Purpose: Splits the address into its 6 bytes.
    // Input:   nothing.
    // Output:  an array of 6 bytes, the first byte at index 0.
    //          Each line moves one byte down to the bottom (">>") and keeps only it ("& 0xFF").
    public byte[] GetBytes() =>
    [
        (byte)((_value >> 40) & 0xFF),   // byte 1
        (byte)((_value >> 32) & 0xFF),   // byte 2
        (byte)((_value >> 24) & 0xFF),   // byte 3
        (byte)((_value >> 16) & 0xFF),   // byte 4
        (byte)((_value >> 8) & 0xFF),    // byte 5
        (byte)(_value & 0xFF),           // byte 6
    ];

    // Purpose: Reads an address from text, in any common form:
    //          00:1A:2B:3C:4D:5E   00-1A-2B-3C-4D-5E   001A.2B3C.4D5E   001A2B3C4D5E
    // Input:   text - the address as text.
    // Output:  the MacAddress. Throws DomainException when the text is not a valid address.
    public static MacAddress Parse(string? text) =>
        TryParse(text, out var result)                                         // try to read it
            ? result                                                           // worked - return it
            : throw new DomainException($"'{text}' is not a valid MAC address.");   // failed - throw

    // Purpose: The same as Parse, but without throwing.
    // Input:   text - the address as text; result - "out" = the method fills it for the caller.
    // Output:  true + the address in result, or false when the text is not a valid address.
    public static bool TryParse(string? text, out MacAddress result)
    {
        // Start with an empty result (needed even when we return false)
        result = default;

        // No text at all - not an address
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        // The number we build, one hex digit at a time
        ulong value = 0;

        // How many hex digits we have read so far (a MAC has exactly 12)
        var digits = 0;

        // Go over every character of the text
        foreach (var ch in text)
        {
            // Separators - skip them
            if (ch is ':' or '-' or '.' or ' ')
            {
                continue;
            }

            // Already 12 digits, or not a hex digit (0-9, A-F) - not a valid address
            if (digits >= 12 || !Uri.IsHexDigit(ch))
            {
                return false;
            }

            // Make room for 4 new bits ("<< 4") and put the digit's value there ("|")
            value = (value << 4) | (uint)Uri.FromHex(ch);

            // One more digit read
            digits++;
        }

        // Fewer than 12 digits - not a valid address
        if (digits != 12)
        {
            return false;
        }

        // Valid - build the address
        result = new MacAddress(value);
        return true;
    }

    // Purpose: Makes a new random address for a new port.
    //          The "made up locally" bit is turned on and the "group" bit is turned off, so it can
    //          never be the same as a real network card's address.
    // Input:   nothing.
    // Output:  a new random single-port (unicast) address.
    public static MacAddress CreateRandomUnicast()
    {
        // Room for 6 bytes. "stackalloc" = a small temporary array that is freed by itself at the end
        Span<byte> bytes = stackalloc byte[6];

        // Fill the 6 bytes with random values
        Random.Shared.NextBytes(bytes);

        // First byte: "& 0xFC" turns off the 2 lowest bits, "| 0x02" turns on the "locally made" bit
        bytes[0] = (byte)((bytes[0] & 0xFC) | 0x02);

        // Join the 6 bytes into one number
        ulong value = 0;
        foreach (var b in bytes)
        {
            // Make room for 8 bits and put the byte there
            value = (value << 8) | b;
        }

        // Build the address
        return new MacAddress(value);
    }

    // Purpose: Writes the address as text.
    // Input:   nothing.
    // Output:  e.g. "02:1A:2B:3C:4D:5E". "X2" = two hex digits in capitals; Join puts ":" between them
    public override string ToString() => string.Join(':', GetBytes().Select(b => b.ToString("X2")));
}
