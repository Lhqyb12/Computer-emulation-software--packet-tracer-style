namespace NetSim.Application.Routing;

/// <summary>
/// What <see cref="IRouterForwardingService.ForwardOneHop"/> actually managed to do with a packet
/// at a router - the <see cref="Core.Routing.RoutingOutcome"/> decision plus the extra ways the
/// <em>on-the-wire</em> step (ARP for the next hop, the outgoing transmission) can fail.
/// </summary>
public enum RouterForwardingOutcome
{
    /// <summary>The packet was routed and transmitted toward its next hop; see <see cref="RouterForwardingResult.Delivery"/>.</summary>
    Forwarded,

    /// <summary>The packet is addressed to the router itself - the caller should deliver it up the local stack.</summary>
    LocalDelivery,

    /// <summary>No route (and no default route) - the packet was dropped; an ICMP Network Unreachable was generated.</summary>
    NoRoute,

    /// <summary>The TTL expired in transit - the packet was dropped; an ICMP Time Exceeded was generated.</summary>
    TtlExpired,

    /// <summary>A route was found but the next hop's MAC could not be resolved by ARP.</summary>
    ArpFailed,

    /// <summary>The ingress interface was not operational.</summary>
    InterfaceDown,

    /// <summary>The packet was dropped for another reason (invalid packet, broadcast/multicast destination, dead outgoing link).</summary>
    Dropped,
}
