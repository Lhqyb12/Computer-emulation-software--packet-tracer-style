using NetSim.Core.Networking;

namespace NetSim.Core.Routing;

/// <summary>
/// The concrete forwarding facts a matched <see cref="Route"/> resolves to (Phase 28): the
/// interface a packet actually leaves through and the IPv4 address ARP must resolve for the next
/// Layer 2 hop.
///
/// <para>For a directly connected route this is trivial - the interface is the route's own
/// interface and the L2 next hop is the packet's destination. For a static <em>next-hop</em> route
/// with no exit interface the router has to work out how to reach the next-hop router first
/// (<see cref="RouteResolver"/> follows the chain of routes down to a connected one); the resolved
/// interface is that connected route's interface, and the L2 next hop stays the address ARP has to
/// resolve on that link - the next-hop router, never the final destination (brief section 11).</para>
/// </summary>
public sealed class RouteResolution
{
    public RouteResolution(Route matchedRoute, NetworkInterface outgoingInterface, IPv4Address l2NextHopAddress)
    {
        ArgumentNullException.ThrowIfNull(matchedRoute);
        ArgumentNullException.ThrowIfNull(outgoingInterface);
        MatchedRoute = matchedRoute;
        OutgoingInterface = outgoingInterface;
        L2NextHopAddress = l2NextHopAddress;
    }

    /// <summary>The route that was selected for the destination (the most specific usable match).</summary>
    public Route MatchedRoute { get; }

    /// <summary>The interface the packet is forwarded out of - always an operational interface of the router.</summary>
    public NetworkInterface OutgoingInterface { get; }

    /// <summary>
    /// The IPv4 address ARP resolves for the next Layer 2 hop: the static route's next-hop router,
    /// or - for a directly connected / exit-interface route - the address being reached on that link.
    /// </summary>
    public IPv4Address L2NextHopAddress { get; }
}
