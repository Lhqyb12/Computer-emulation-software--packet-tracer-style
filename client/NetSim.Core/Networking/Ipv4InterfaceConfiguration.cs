using NetSim.Core.Common.Exceptions;     // brings in DomainException - the error Core throws when a rule is broken

namespace NetSim.Core.Networking;   // the "address" of this file: the Networking part of Core

// The IPv4 settings of one port (step 10): its address AND its subnet mask, kept together - one without the other means
// nothing (the address says "who am I", the mask says "which network am I in"). Written short as "192.168.1.10/24".
// It can only be built through Create, which refuses addresses a port must not have, so a port can never hold a bad one.
// Taken from the reference project's Ipv4InterfaceConfiguration. Changes:
//   - one address per port (the reference allowed several, "primary" and "secondary") - enough for everything we build;
//   - three more rules (marked "new"): no loopback (127.x.x.x), and - for masks up to /30 - not the network's own address
//     and not its broadcast address. Packet Tracer refuses those too, and they are classic exam questions.
// "sealed" = no other class can inherit from it
public sealed class Ipv4InterfaceConfiguration
{
    // Purpose:       Builds the settings from values that were already checked.
    // Precondition:  receives address, prefixLength, subnetMask - already checked by Create (below).
    // Postcondition: returns nothing (a constructor) - the three values are kept.
    // "private" = nobody outside can call it - the only way in is Create, which checks everything first
    private Ipv4InterfaceConfiguration(IPv4Address address, int prefixLength, SubnetMask subnetMask)
    {
        Address = address;            // the port's address, e.g. 192.168.1.10
        PrefixLength = prefixLength;  // how many 1-bits the mask has, e.g. 24
        SubnetMask = subnetMask;      // the mask itself, e.g. 255.255.255.0
    }

    // The port's address. Only "get" = set once in the constructor, never changed
    public IPv4Address Address { get; }

    // The mask's prefix length (its number of 1-bits), e.g. 24
    public int PrefixLength { get; }

    // The subnet mask, e.g. 255.255.255.0
    public SubnetMask SubnetMask { get; }

    // The short way to write both together, e.g. "192.168.1.10/24". "=>" = worked out every time it is read
    public string Cidr => $"{Address}/{PrefixLength}";

    // Purpose:       Builds a port's IPv4 settings from an address and a prefix length - checking every rule first.
    // Precondition:  receives address - the port's address; prefixLength - the mask's number of 1-bits (0 to 32).
    // Postcondition: returns the settings. Throws DomainException when the address may not be given to a port, or the
    //                prefix length is outside 0-32 (the message says exactly which rule was broken).
    // "static" = called through the class itself: Ipv4InterfaceConfiguration.Create(address, 24)
    public static Ipv4InterfaceConfiguration Create(IPv4Address address, int prefixLength)
    {
        // Rule 1: 0.0.0.0 means "no address" - it can't be a port's address
        if (address.IsUnspecified)
        {
            throw new DomainException("A port's IPv4 address cannot be 0.0.0.0.");
        }

        // Rule 2: 255.255.255.255 means "everyone" - it can't be one port's address
        if (address.IsLimitedBroadcast)
        {
            throw new DomainException("A port's IPv4 address cannot be 255.255.255.255 (the broadcast address).");
        }

        // Rule 3: 224.x.x.x - 239.x.x.x are group addresses - not one port's address
        if (address.IsMulticast)
        {
            throw new DomainException($"A port's IPv4 address cannot be a multicast address ({address}).");
        }

        // Rule 4 (new): 127.x.x.x means "this very computer" - it can't be given to a port
        if (address.IsLoopback)
        {
            throw new DomainException($"A port's IPv4 address cannot be a loopback address ({address}).");
        }

        // Build the mask from the prefix length (SubnetMask.FromPrefixLength, part 10.2).
        // It also checks the prefix length is 0-32, and throws DomainException when it is not
        var subnetMask = SubnetMask.FromPrefixLength(prefixLength);

        // Rules 5 and 6 (new) - only for masks up to /30. In /31 and /32 there are only 1 or 2 addresses in the network,
        // so there is no separate "network address" or "broadcast address" to keep free
        if (prefixLength <= 30)
        {
            // The DEVICE part of the address = the bits where the mask is 0.
            // "~subnetMask.Value" flips the mask (its 0s become 1s); "&" with the address keeps only the device bits.
            // E.g. 192.168.1.10/24: device bits = 00001010 = 10
            var hostBits = address.Value & ~subnetMask.Value;

            // Rule 5: the device part is all 0s -> this is the NETWORK's own address (e.g. 192.168.1.0/24)
            if (hostBits == 0)
            {
                throw new DomainException($"{address}/{prefixLength} is the network's own address - give the port another one.");
            }

            // Rule 6: the device part is all 1s (it equals the flipped mask) -> this is the network's BROADCAST address,
            // "everyone in this network" (e.g. 192.168.1.255/24)
            if (hostBits == ~subnetMask.Value)
            {
                throw new DomainException($"{address}/{prefixLength} is the network's broadcast address - give the port another one.");
            }
        }

        // Every rule passed - build the settings (through the private constructor) and hand them back
        return new Ipv4InterfaceConfiguration(address, prefixLength, subnetMask);
    }

    // Purpose:       Builds a port's IPv4 settings from an address and a subnet mask (instead of a prefix length).
    // Precondition:  receives address - the port's address; subnetMask - an already valid mask.
    // Postcondition: returns the settings, or throws DomainException exactly like the Create above.
    // It simply turns the mask into its prefix length and calls the Create above, so every rule lives in ONE place
    public static Ipv4InterfaceConfiguration Create(IPv4Address address, SubnetMask subnetMask) =>
        Create(address, subnetMask.PrefixLength);

    // Purpose:       Writes the settings as text, e.g. "192.168.1.10/24".
    // Precondition:  receives nothing.
    // Postcondition: returns the short form (Cidr).
    public override string ToString() => Cidr;
}