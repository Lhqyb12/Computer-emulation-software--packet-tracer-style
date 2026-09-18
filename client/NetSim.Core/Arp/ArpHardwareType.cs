namespace NetSim.Core.Arp;

/// <summary>
/// The ARP <c>htype</c> field - the link-layer technology the hardware addresses belong to.
/// Modelled as a value object (not an enum) with the same rationale as
/// <see cref="Ethernet.EtherType"/>: a packet can faithfully carry any value, and only the one
/// this phase supports - Ethernet (1) - is given meaning.
/// </summary>
public readonly record struct ArpHardwareType(ushort Value)
{
    /// <summary>Ethernet (10Mb) - IANA hardware type 1. The only type this phase supports.</summary>
    public static ArpHardwareType Ethernet => new(1);

    /// <summary>True for <see cref="Ethernet"/> (value 1).</summary>
    public bool IsEthernet => Value == 1;

    /// <summary>A short human-readable label for diagnostics and a future inspector UI.</summary>
    public string Name => Value switch
    {
        1 => "Ethernet",
        _ => "Unknown",
    };

    public override string ToString() => $"{Name} ({Value})";
}
