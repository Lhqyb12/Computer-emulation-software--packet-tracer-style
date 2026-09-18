namespace NetSim.Core.Icmp;

/// <summary>
/// The 8-bit Code field of an <see cref="IcmpMessage"/> - its meaning depends on the message's
/// <see cref="IcmpType"/> (a Destination Unreachable code names a reason, a Time Exceeded code
/// names which timer expired, an Echo Request/Reply code is always 0). Modelled as a value object,
/// same rationale as <see cref="IcmpType"/>, so any value round-trips even if this project has not
/// named it. The named constants are the extensible catalogue the brief asks for; more can be added
/// later (e.g. further Destination Unreachable reasons) without changing <see cref="IcmpMessage"/>.
/// </summary>
public readonly record struct IcmpCode
{
    public IcmpCode(byte value) => Value = value;

    public byte Value { get; }

    /// <summary>The only code Echo Request / Echo Reply use.</summary>
    public static IcmpCode Zero => new(0);

    // ---- Destination Unreachable (RFC 792) ----

    public static IcmpCode NetworkUnreachable => new(0);

    public static IcmpCode HostUnreachable => new(1);

    public static IcmpCode ProtocolUnreachable => new(2);

    public static IcmpCode PortUnreachable => new(3);

    // ---- Time Exceeded (RFC 792) ----

    /// <summary>TTL reached zero in transit - the only Time Exceeded code a (future) forwarding engine generates.</summary>
    public static IcmpCode TimeToLiveExceededInTransit => new(0);

    /// <summary>A short human-readable label for diagnostics and a future inspector UI, given the message's type.</summary>
    public string NameFor(IcmpType type) => (type.Value, Value) switch
    {
        (0, 0) => "Echo Reply",
        (8, 0) => "Echo Request",
        (3, 0) => "Network Unreachable",
        (3, 1) => "Host Unreachable",
        (3, 2) => "Protocol Unreachable",
        (3, 3) => "Port Unreachable",
        (11, 0) => "TTL Exceeded in Transit",
        _ => $"Code {Value}",
    };

    public override string ToString() => Value.ToString();
}
