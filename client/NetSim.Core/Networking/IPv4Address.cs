using System.Globalization;              // brings in CultureInfo and NumberStyles - read / write numbers the same way on every computer
using NetSim.Core.Common.Exceptions;     // brings in DomainException - the error Core throws when a rule is broken

namespace NetSim.Core.Networking;   // the "address" of this file: the Networking part of Core

// An IPv4 address (step 10): four numbers from 0 to 255 with dots between them, e.g. 192.168.1.10.
// Inside it is kept as ONE 32-bit number (uint): each of the four parts takes 8 bits -
//   192.168.1.10  ->  11000000 10101000 00000001 00001010  (= 3232235786)
// Kept as a number (not text) because later steps CALCULATE with addresses - e.g. "are these two in the same network?"
// is done with the subnet mask on the numbers (part 10.2).
// Taken from the reference project's IPv4Address, cut to what steps 10-11 need (no byte arrays, sorting or categories yet).
// "readonly record struct" = a small value that never changes after it is built, and two addresses with the same
// number count as EQUAL (compared by value) - exactly like CanvasPoint (step 2)
public readonly record struct IPv4Address
{
    // The address as one 32-bit number. "uint" = a whole number with no minus sign, from 0 to 4,294,967,295 -
    // exactly 32 bits. "private readonly" = only this type sees it, and it never changes after the address is built
    private readonly uint _value;

    // Purpose:       Builds an address from its 32-bit number.
    // Precondition:  receives value - the whole address as one number (any uint is a valid address).
    // Postcondition: returns nothing (a constructor) - the address holds this number.
    // "=>" = a one-line body: just keep the number
    public IPv4Address(uint value) => _value = value;

    // The address as its 32-bit number (the subnet mask, part 10.2, calculates with it)
    public uint Value => _value;

    // The four parts of the address, each 0-255, taken out of the 32-bit number with two bit tricks:
    //   ">> 24"  = shift the bits 24 places to the right, so the wanted 8 bits end up at the bottom;
    //   "& 0xFF" = keep only the bottom 8 bits (0xFF = 11111111 = 255) and drop the rest;
    //   "(byte)" = turn the result into a byte (a number 0-255).
    // Example for 192.168.1.10: Octet1 = 192, Octet2 = 168, Octet3 = 1, Octet4 = 10
    private byte Octet1 => (byte)((_value >> 24) & 0xFF);   // the first part (the top 8 bits)
    private byte Octet2 => (byte)((_value >> 16) & 0xFF);   // the second part
    private byte Octet3 => (byte)((_value >> 8) & 0xFF);    // the third part
    private byte Octet4 => (byte)(_value & 0xFF);           // the fourth part (the bottom 8 bits - no shift needed)

    // ----- Special addresses that may NOT be given to a port (Ipv4InterfaceConfiguration checks them, part 10.3) -----

    // 0.0.0.0 - "no address" (all 32 bits are 0)
    public bool IsUnspecified => _value == 0;

    // 255.255.255.255 - "everyone on this network" (all 32 bits are 1; 0xFFFF_FFFF = 32 ones; "_" only makes it readable)
    public bool IsLimitedBroadcast => _value == 0xFFFF_FFFFu;

    // 224.x.x.x - 239.x.x.x - a group address (one message to many devices).
    // "& 0xF000_0000" keeps only the top 4 bits; for these addresses they are always 1110 (= 0xE)
    public bool IsMulticast => (_value & 0xF000_0000u) == 0xE000_0000u;

    // 127.x.x.x - "this very computer" (loopback). "& 0xFF00_0000" keeps only the first part; 0x7F = 127
    public bool IsLoopback => (_value & 0xFF00_0000u) == 0x7F00_0000u;

    // Purpose:       Reads an address from text, e.g. what the user typed in the properties panel.
    // Precondition:  receives text - the text to read (may be null, empty or wrong).
    // Postcondition: returns the address. Throws DomainException when the text is not a valid IPv4 address.
    // "a ? b : c" = if TryParse worked, hand back "result"; otherwise throw the error
    public static IPv4Address Parse(string? text) =>
        TryParse(text, out var result)
            ? result
            : throw new DomainException($"'{text}' is not a valid IPv4 address.");

    // Purpose:       Tries to read an address from text, WITHOUT throwing when the text is wrong.
    // Precondition:  receives text - the text to read (may be null, empty or wrong); result - an "out" box it fills.
    // Postcondition: returns true and fills result with the address when the text is exactly four numbers 0-255 with
    //                dots between them; otherwise returns false (and result is 0.0.0.0).
    // "static" = called through the type itself: IPv4Address.TryParse("10.0.0.1", out var address)
    public static bool TryParse(string? text, out IPv4Address result)
    {
        // An "out" parameter must get a value on every way out - start with the empty address (0.0.0.0)
        result = default;

        // null, "" or only spaces - not an address
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;   // stop here
        }

        // Cut the text at every dot: "192.168.1.10" -> ["192", "168", "1", "10"]
        var parts = text.Split('.');

        // There must be exactly four parts ("1.2.3" or "1.2.3.4.5" are not addresses)
        if (parts.Length != 4)
        {
            return false;   // stop here
        }

        // Build the 32-bit number part by part, starting from 0
        uint value = 0;

        // Go over the four parts in order
        foreach (var part in parts)
        {
            // Read the part as a byte - a number from 0 to 255. If it is not (e.g. "256", "abc", "-1", "" or " 1"),
            // the text is not an address.
            // NumberStyles.None = only plain digits: no spaces, no sign. CultureInfo.InvariantCulture = the same rules on
            // every computer, whatever its language. "out var octet" = the number it read
            if (!byte.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var octet))
            {
                return false;   // stop here
            }

            // Make room for the new part and put it in:
            //   "value << 8" = shift everything 8 places to the LEFT (the parts read so far move up);
            //   "| octet"    = put the new part into the 8 free bits at the bottom.
            // After 192, 168, 1, 10 the number holds all four, in order
            value = (value << 8) | octet;
        }

        // All four parts were fine - build the address and hand it out
        result = new IPv4Address(value);
        return true;
    }

    // Purpose:       Writes the address as text, e.g. "192.168.1.10".
    // Precondition:  receives nothing.
    // Postcondition: returns the four parts with dots between them.
    // "override" = replaces the ToString that every C# value already has. string.Create with InvariantCulture = written the
    // same way on every computer; "$" puts the four parts inside the text
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Octet1}.{Octet2}.{Octet3}.{Octet4}");
}