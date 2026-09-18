namespace NetSim.Core.IP;

/// <summary>
/// The 8-bit Next Header field of an <see cref="IPv6Header"/> (or of an
/// <see cref="IPv6ExtensionHeader"/>). It identifies whatever comes next in the packet - either an
/// <em>extension header</em> (hop-by-hop options, routing, fragment, destination options) or an
/// <em>upper-layer protocol</em> (ICMPv6, TCP, UDP, ...). Modelled as a value object rather than an
/// enum (the same choice as <see cref="Ethernet.EtherType"/> / <see cref="ProtocolNumber"/>) so a
/// packet can faithfully represent <em>any</em> value, including ones this simulation does not
/// implement yet: <see cref="IsKnown"/> is then false and <see cref="Name"/> is "Unknown", and
/// nothing fails.
///
/// The values share IANA's protocol-number space with <see cref="ProtocolNumber"/>; the well-known
/// constants exist for readability and for a future extension-header / upper-layer dispatch step
/// (ICMPv6, TCP/UDP, and real extension headers all arrive in later phases). The IPv6 layer itself
/// performs no protocol-specific logic based on this value in this phase.
/// </summary>
public readonly record struct NextHeader
{
    public NextHeader(byte value) => Value = value;

    public byte Value { get; }

    /// <summary>Hop-by-Hop Options extension header (0). Not implemented yet.</summary>
    public static NextHeader HopByHopOptions => new(0);

    /// <summary>TCP (6). Not implemented until Phase 22.</summary>
    public static NextHeader Tcp => new(6);

    /// <summary>UDP (17). Not implemented until Phase 22.</summary>
    public static NextHeader Udp => new(17);

    /// <summary>Routing extension header (43). Not implemented yet.</summary>
    public static NextHeader Routing => new(43);

    /// <summary>Fragment extension header (44). Not implemented (no fragmentation this project).</summary>
    public static NextHeader Fragment => new(44);

    /// <summary>ICMPv6 (58). Not implemented until a later IPv6/ICMPv6 phase.</summary>
    public static NextHeader IcmpV6 => new(58);

    /// <summary>"No Next Header" (59) - nothing follows this header.</summary>
    public static NextHeader None => new(59);

    /// <summary>Destination Options extension header (60). Not implemented yet.</summary>
    public static NextHeader DestinationOptions => new(60);

    /// <summary>True for one of the well-known values this project has names for.</summary>
    public bool IsKnown => Value is 0 or 6 or 17 or 43 or 44 or 58 or 59 or 60;

    /// <summary>
    /// True when this value names an IPv6 extension header rather than an upper-layer protocol
    /// (hop-by-hop options, routing, fragment, destination options). Used only to walk the header
    /// chain; no extension header is processed in this phase.
    /// </summary>
    public bool IsExtensionHeader => Value is 0 or 43 or 44 or 60;

    /// <summary>True when this value names an upper-layer protocol (i.e. it terminates the header chain).</summary>
    public bool IsUpperLayer => !IsExtensionHeader && Value != 59;

    /// <summary>A short human-readable label for diagnostics and a future inspector UI.</summary>
    public string Name => Value switch
    {
        0 => "Hop-by-Hop Options",
        6 => "TCP",
        17 => "UDP",
        43 => "Routing",
        44 => "Fragment",
        58 => "ICMPv6",
        59 => "No Next Header",
        60 => "Destination Options",
        _ => "Unknown",
    };

    public override string ToString() => $"{Name} ({Value})";
}
