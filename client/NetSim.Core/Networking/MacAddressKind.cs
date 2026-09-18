namespace NetSim.Core.Networking;

/// <summary>
/// The three delivery classes an Ethernet destination <see cref="MacAddress"/> falls into. These
/// are treated as mutually exclusive here (a broadcast address is reported as
/// <see cref="Broadcast"/>, not <see cref="Multicast"/>) so a forwarding decision in a later
/// phase can branch cleanly. Address <em>classification</em> only - no multicast group or
/// protocol behaviour is modelled.
/// </summary>
public enum MacAddressKind
{
    /// <summary>A single specific interface - the I/G bit of the first octet is 0.</summary>
    Unicast,

    /// <summary>A group of interfaces - the I/G bit of the first octet is 1 (and it is not the broadcast address).</summary>
    Multicast,

    /// <summary>Every interface on the segment - the address FF:FF:FF:FF:FF:FF.</summary>
    Broadcast,
}
