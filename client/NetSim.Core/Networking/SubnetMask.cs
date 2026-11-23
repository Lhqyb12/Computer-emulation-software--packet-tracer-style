using System.Numerics;                   // brings in BitOperations.PopCount - counts how many bits are 1 in a number
using NetSim.Core.Common.Exceptions;     // brings in DomainException - the error Core throws when a rule is broken

namespace NetSim.Core.Networking;   // the "address" of this file: the Networking part of Core

// An IPv4 subnet mask (step 10), e.g. 255.255.255.0. It splits an address into two parts:
//   the 1-bits  = the NETWORK part ("which neighbourhood"),
//   the 0-bits  = the DEVICE part ("which house in it").
//   255.255.255.0  ->  11111111 11111111 11111111 00000000  = 24 ones = "/24" (its prefix length)
// Rule: in a mask all the 1s come FIRST and all the 0s after them ("contiguous"); 255.0.255.0 is NOT a mask.
// Later (step 20) the mask tells whether two addresses are in the same network: send straight, or through the gateway.
// Taken from the reference project's SubnetMask, cut to what steps 10-11 need.
// "readonly record struct" = a small value that never changes, compared by its value (like IPv4Address)
public readonly record struct SubnetMask
{
    // The mask as one 32-bit number, like IPv4Address. "private readonly" = only this type sees it, never changes
    private readonly uint _value;

    // Purpose:       Builds a mask from its 32-bit number.
    // Precondition:  receives value - the mask as one number (already checked to be contiguous by whoever calls this).
    // Postcondition: returns nothing (a constructor) - the mask holds this number.
    // "private" = only this type can build a mask straight from a number; everyone else must go through FromPrefixLength
    // or TryParse, which make sure the mask is valid
    private SubnetMask(uint value) => _value = value;

    // The mask as its 32-bit number
    public uint Value => _value;

    // How many 1-bits the mask has = its prefix length, e.g. 255.255.255.0 -> 24.
    // BitOperations.PopCount = "population count": counts the 1-bits in a number (in a valid mask they are all at the front)
    public int PrefixLength => BitOperations.PopCount(_value);

    // The mask written as an address (the same 32 bits), so it can be shown as text like 255.255.255.0
    public IPv4Address AsAddress => new(_value);

    // Purpose:       Builds a mask from its prefix length, e.g. 24 -> 255.255.255.0.
    // Precondition:  receives prefixLength - how many 1-bits the mask starts with (must be 0 to 32).
    // Postcondition: returns the mask. Throws DomainException when prefixLength is outside 0-32.
    public static SubnetMask FromPrefixLength(int prefixLength)
    {
        // "is < 0 or > 32" = smaller than 0 OR bigger than 32 - there are only 32 bits
        if (prefixLength is < 0 or > 32)
        {
            throw new DomainException($"An IPv4 prefix length must be between 0 and 32, but was {prefixLength}.");
        }

        // Build the number: start from 32 ones (0xFFFF_FFFF) and shift them LEFT by (32 - prefixLength) places - the places
        // they leave at the bottom fill with 0s. E.g. 24: shift by 8 -> 11111111 11111111 11111111 00000000.
        // Prefix 0 is a special case: shifting a 32-bit number by 32 places is not allowed in C#, so it is written as 0 directly
        var value = prefixLength == 0 ? 0u : 0xFFFF_FFFFu << (32 - prefixLength);

        // Hand back the mask
        return new SubnetMask(value);
    }

    // Purpose:       Reads a mask from text (e.g. what the user typed), throwing when it is wrong.
    // Precondition:  receives text - the text to read (may be null, empty or wrong).
    // Postcondition: returns the mask. Throws DomainException when the text is not a valid, contiguous mask.
    public static SubnetMask Parse(string? text) =>
        TryParse(text, out var result)
            ? result
            : throw new DomainException($"'{text}' is not a valid, contiguous IPv4 subnet mask.");

    // Purpose:       Tries to read a mask from text, WITHOUT throwing when the text is wrong.
    // Precondition:  receives text - the text to read (may be null, empty or wrong); result - an "out" box it fills.
    // Postcondition: returns true and fills result when the text is an address whose 1-bits all come first; otherwise false.
    public static bool TryParse(string? text, out SubnetMask result)
    {
        // An "out" parameter must get a value on every way out - start with the empty mask
        result = default;

        // Two things must both hold ("||" = if EITHER fails, it is not a mask):
        //   1. the text is written like an address (IPv4Address.TryParse, part 10.1) - "out var address" = what it read;
        //   2. its bits are contiguous - all 1s first, then all 0s (IsContiguous, below)
        if (!IPv4Address.TryParse(text, out var address) || !IsContiguous(address.Value))
        {
            return false;   // stop here
        }

        // Both hold - build the mask from the same 32 bits and hand it out
        result = new SubnetMask(address.Value);
        return true;
    }

    // Purpose:       Answers whether a 32-bit number is a valid mask: all its 1-bits come first, then all 0-bits.
    // Precondition:  receives value - the number to check.
    // Postcondition: returns true when it is contiguous (including all 0s and all 1s), otherwise false.
    // The trick, in two steps (example 255.255.255.0):
    //   1. "~value" flips every bit: a valid mask (ones then zeros) becomes zeros then ones -
    //        11111111 11111111 11111111 00000000  ->  00000000 00000000 00000000 11111111
    //   2. A number that is "zeros then ones" plus 1 has NO 1-bit in common with itself:
    //        00000000 00000000 00000000 11111111  +1  ->  00000000 00000000 00000001 00000000
    //        and "&" of the two = 0  -> contiguous.
    //      For a broken mask like 255.0.255.0 the flipped number has ones in two places, adding 1 does not clear them
    //      all, and "&" is not 0 -> not contiguous
    public static bool IsContiguous(uint value)
    {
        var inverted = ~value;                          // flip every bit ("~" = NOT, bit by bit)
        return (inverted & (inverted + 1)) == 0;        // no shared 1-bit between it and itself + 1 = contiguous
    }

    // Purpose:       Writes the mask as text, e.g. "255.255.255.0".
    // Precondition:  receives nothing.
    // Postcondition: returns the mask written like an address.
    // It simply turns the mask into an address (the same 32 bits) and uses IPv4Address's own ToString (part 10.1)
    public override string ToString() => AsAddress.ToString();
}