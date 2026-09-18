namespace NetSim.Core.Routing;

/// <summary>
/// What the <see cref="IRoutingEngine"/> decided to do with an IPv4 packet that arrived at a
/// router. Every terminal state a Layer 3 forwarding decision can reach (brief section 39), so a
/// caller and future diagnostics never have to infer intent from a boolean.
/// </summary>
public enum RoutingOutcome
{
    /// <summary>The packet is addressed to the router itself - hand it to the local protocol stack (ICMP echo, ...). Never forwarded.</summary>
    LocalDelivery,

    /// <summary>The packet is for somewhere else and a route was found - forward it (TTL already decremented on <see cref="RoutingResult.ForwardedPacket"/>).</summary>
    Forward,

    /// <summary>No matching route and no default route - drop the packet (ICMP Network Unreachable is appropriate).</summary>
    NoRoute,

    /// <summary>The TTL would reach zero on this hop - drop the packet (ICMP Time Exceeded is appropriate).</summary>
    TtlExpired,

    /// <summary>The packet failed IPv4 structural validation - drop it.</summary>
    InvalidPacket,

    /// <summary>The ingress interface is administratively down or its link is down - the packet cannot be processed.</summary>
    InterfaceDown,

    /// <summary>The packet was dropped for another reason stated in <see cref="RoutingResult.Reason"/> (e.g. a broadcast/multicast destination the router does not bridge).</summary>
    Drop,
}
