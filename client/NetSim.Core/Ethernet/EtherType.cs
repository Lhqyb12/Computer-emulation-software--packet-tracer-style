namespace NetSim.Core.Ethernet;

/// <summary>
/// The 16-bit EtherType field of an <see cref="EthernetFrame"/> - it identifies the protocol
/// carried in the frame's payload. Modelled as a value object rather than an enum so that a
/// frame can faithfully represent <em>any</em> value seen on the wire, including protocols this
/// simulation does not implement yet (<see cref="IsKnown"/> is false, <see cref="Name"/> is
/// "Unknown") - the Ethernet layer must never fail just because the upper-layer protocol has not
/// been built. The well-known constants exist for readability and for a future upper-layer
/// dispatch step (Phase 18+); the Ethernet layer itself performs no protocol-specific logic
/// based on this value.
/// </summary>
public readonly record struct EtherType
{
    /// <summary>Ethernet II frames carry an EtherType (a length otherwise, for 802.3) when this value or greater.</summary>
    public const ushort EthernetIIMinimum = 0x0600;

    public EtherType(ushort value) => Value = value;

    public ushort Value { get; }

    /// <summary>The "not set" EtherType (0x0000) - rejected by <see cref="EthernetFrame"/> validation.</summary>
    public static EtherType Unspecified => new(0x0000);

    /// <summary>IPv4 (0x0800). The protocol itself is not implemented until Phase 18.</summary>
    public static EtherType IPv4 => new(0x0800);

    /// <summary>ARP (0x0806). Not implemented until Phase 20.</summary>
    public static EtherType Arp => new(0x0806);

    /// <summary>IPv6 (0x86DD). Not implemented until Phase 19.</summary>
    public static EtherType IPv6 => new(0x86DD);

    /// <summary>True when <see cref="Value"/> is a non-zero value.</summary>
    public bool IsSpecified => Value != 0;

    /// <summary>True for one of the well-known assigned values this project has names for.</summary>
    public bool IsKnown => Value is 0x0800 or 0x0806 or 0x86DD;

    /// <summary>True when the value is in the Ethernet II EtherType range (0x0600 and above).</summary>
    public bool IsEthernetII => Value >= EthernetIIMinimum;

    /// <summary>A short human-readable label for diagnostics and a future inspector UI.</summary>
    public string Name => Value switch
    {
        0x0000 => "Unspecified",
        0x0800 => "IPv4",
        0x0806 => "ARP",
        0x86DD => "IPv6",
        _ => "Unknown",
    };

    public override string ToString() => $"{Name} (0x{Value:X4})";
}
