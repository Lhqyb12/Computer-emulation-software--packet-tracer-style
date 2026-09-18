namespace NetSim.Core.Icmp;

/// <summary>
/// The 8-bit Type field of an <see cref="IcmpMessage"/>. Modelled as a value object rather than an
/// enum - the same choice as <see cref="Ethernet.EtherType"/> / <see cref="IP.ProtocolNumber"/> - so
/// a message can faithfully represent any value, including a type this simulation does not
/// implement (<see cref="IsKnown"/> is then false and <see cref="Name"/> is "Unknown"). Only the
/// four types Phase 21 models are given meaning: <see cref="EchoReply"/> (0),
/// <see cref="DestinationUnreachable"/> (3), <see cref="EchoRequest"/> (8) and
/// <see cref="TimeExceeded"/> (11) - the numeric values are the RFC 792 assignments.
/// </summary>
public readonly record struct IcmpType
{
    public IcmpType(byte value) => Value = value;

    public byte Value { get; }

    /// <summary>Echo Reply (0) - the answer to an <see cref="EchoRequest"/>.</summary>
    public static IcmpType EchoReply => new(0);

    /// <summary>Destination Unreachable (3). Foundation only in this phase - no routing/transport layer generates it yet.</summary>
    public static IcmpType DestinationUnreachable => new(3);

    /// <summary>Echo Request (8) - what a ping sends.</summary>
    public static IcmpType EchoRequest => new(8);

    /// <summary>Time Exceeded (11). Foundation only in this phase - no forwarding engine decrements TTL to zero yet.</summary>
    public static IcmpType TimeExceeded => new(11);

    /// <summary>True for one of the four types this project has names for.</summary>
    public bool IsKnown => Value is 0 or 3 or 8 or 11;

    /// <summary>A short human-readable label for diagnostics and a future inspector UI.</summary>
    public string Name => Value switch
    {
        0 => "Echo Reply",
        3 => "Destination Unreachable",
        8 => "Echo Request",
        11 => "Time Exceeded",
        _ => "Unknown",
    };

    public override string ToString() => $"{Name} ({Value})";
}
