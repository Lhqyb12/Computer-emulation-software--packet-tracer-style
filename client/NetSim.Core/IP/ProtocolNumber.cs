namespace NetSim.Core.IP;

/// <summary>
/// The 8-bit Protocol field of an <see cref="IPv4Header"/> - it identifies the protocol carried in
/// the IPv4 payload. Modelled as a value object rather than an enum (the same choice as
/// <see cref="Ethernet.EtherType"/>) so a packet can faithfully represent <em>any</em> protocol
/// number, including ones this simulation does not implement yet: <see cref="IsKnown"/> is then
/// false and <see cref="Name"/> is "Unknown", and nothing fails. The well-known constants exist
/// for readability and for a future upper-layer dispatch step (ICMP in Phase 21, TCP/UDP in
/// Phase 22); the IPv4 layer itself performs no protocol-specific logic based on this value.
/// </summary>
public readonly record struct ProtocolNumber
{
    public ProtocolNumber(byte value) => Value = value;

    public byte Value { get; }

    /// <summary>The "not set" protocol number (0). Used when an IPv4 packet carries a generic payload.</summary>
    public static ProtocolNumber Unspecified => new(0);

    /// <summary>ICMP (1). Not implemented until Phase 21.</summary>
    public static ProtocolNumber Icmp => new(1);

    /// <summary>TCP (6). Not implemented until Phase 22.</summary>
    public static ProtocolNumber Tcp => new(6);

    /// <summary>UDP (17). Not implemented until Phase 22.</summary>
    public static ProtocolNumber Udp => new(17);

    /// <summary>True when the value is non-zero.</summary>
    public bool IsSpecified => Value != 0;

    /// <summary>True for one of the well-known numbers this project has names for.</summary>
    public bool IsKnown => Value is 1 or 6 or 17;

    /// <summary>A short human-readable label for diagnostics and a future inspector UI.</summary>
    public string Name => Value switch
    {
        0 => "Unspecified",
        1 => "ICMP",
        6 => "TCP",
        17 => "UDP",
        _ => "Unknown",
    };

    public override string ToString() => $"{Name} ({Value})";
}
