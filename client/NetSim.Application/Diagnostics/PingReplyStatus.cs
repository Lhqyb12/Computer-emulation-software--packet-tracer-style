namespace NetSim.Application.Diagnostics;

/// <summary>
/// The strongly-typed outcome of one <see cref="PingReply"/> - deliberately richer than a bare
/// success/failure boolean (brief section 21: "do not expose only a boolean"), so the UI can show a
/// specific, understandable message for each way a simulated ping can fail.
/// </summary>
public enum PingReplyStatus
{
    /// <summary>An Echo Reply matching the request's identifier and sequence number was received.</summary>
    Success,

    /// <summary>No Echo Reply arrived - either nobody at the destination answered, or nobody answered the ARP request for it.</summary>
    TimedOut,

    /// <summary>A link-level failure prevented the request (or its reply) from being sent - e.g. the interface or cable is down.</summary>
    DestinationUnreachable,

    /// <summary>
    /// The destination is off the source interface's local subnet and cannot be reached: the source
    /// has no default gateway configured, a router on the path has no route to the destination, or
    /// the reply cannot be routed back.
    /// </summary>
    NoRoute,

    /// <summary>A router on the path decremented the packet's TTL to zero - it was dropped and an ICMP Time Exceeded was generated.</summary>
    TtlExpired,

    /// <summary>ARP could not resolve the destination's MAC address (the sending interface has no MAC/IPv4 to ARP with).</summary>
    ArpResolutionFailed,

    /// <summary>The destination address is not a valid ping target (unspecified, broadcast, or multicast).</summary>
    InvalidDestination,

    /// <summary>The source interface has no IPv4 address configured.</summary>
    NoSourceAddress,

    /// <summary>An unexpected condition occurred while processing the simulated exchange.</summary>
    Error,
}
