using NetSim.Core.Networking;

namespace NetSim.Core.Routing;

/// <summary>
/// The outcome of a single <see cref="IRouteLookup.FindBestRoute"/> call: either the winning
/// <see cref="Route"/> (with the derived facts a forwarder needs pulled out for convenience) or a
/// "no route" answer. Deliberately richer than returning a bare <see cref="Route"/> so routing
/// diagnostics (brief section 41) can explain <em>why</em> a route was or was not chosen.
///
/// <para>Phase 28: a matched route is also <em>resolved</em> - its recursive next hop is followed
/// down to a real outgoing interface (see <see cref="RouteResolver"/>). <see cref="OutgoingInterface"/>
/// and <see cref="NextHopAddress"/> then carry the <em>resolved</em> interface and the address ARP
/// must resolve. A route can match but be currently unusable (its transit interface is down, its
/// next hop is unreachable); then <see cref="HasRoute"/> is still true but
/// <see cref="IsResolvable"/> is false, so a caller can tell "no route at all" from "a configured
/// route that cannot be used right now" (brief section 30).</para>
/// </summary>
public sealed class RouteLookupResult
{
    private RouteLookupResult(
        bool hasRoute, bool isResolvable, Route? route, RouteResolution? resolution,
        IPv4Address destination, string reason)
    {
        HasRoute = hasRoute;
        IsResolvable = isResolvable;
        Route = route;
        Resolution = resolution;
        DestinationAddress = destination;
        Reason = reason;
    }

    /// <summary>The destination address the lookup was performed for.</summary>
    public IPv4Address DestinationAddress { get; }

    /// <summary>True when a route matching the destination exists (it may or may not be usable right now - see <see cref="IsResolvable"/>).</summary>
    public bool HasRoute { get; }

    /// <summary>
    /// True when the matched route resolves to a usable outgoing interface and next hop. False for
    /// a "no route" result, and for a route that matches but is currently unusable.
    /// </summary>
    public bool IsResolvable { get; }

    /// <summary>The selected route, or <c>null</c> when <see cref="HasRoute"/> is false.</summary>
    public Route? Route { get; }

    /// <summary>The concrete forwarding facts the route resolved to, or <c>null</c> when it is not resolvable.</summary>
    public RouteResolution? Resolution { get; }

    /// <summary>
    /// The interface a matching packet would leave through - the <em>resolved</em> interface for a
    /// recursive static route. <c>null</c> when there is no route or it cannot be resolved.
    /// </summary>
    public NetworkInterface? OutgoingInterface => Resolution?.OutgoingInterface ?? Route?.OutgoingInterface;

    /// <summary>
    /// The IPv4 address ARP must resolve for the next Layer 2 hop: the resolved next-hop router for
    /// a route with a next hop, or - for a directly connected route - <see cref="DestinationAddress"/>
    /// itself. <c>null</c> only when there is no route.
    /// </summary>
    public IPv4Address? NextHopAddress =>
        Resolution?.L2NextHopAddress
        ?? (Route is null ? null : Route.NextHop ?? DestinationAddress);

    /// <summary>The matched route's prefix length, or -1 when there is no route.</summary>
    public int PrefixLength => Route?.PrefixLength ?? -1;

    /// <summary>The matched route's type, or <c>null</c> when there is no route.</summary>
    public RouteType? RouteType => Route?.Type;

    /// <summary>True when the matched route reaches the destination network without an intermediate router.</summary>
    public bool IsDirectlyConnected => Route is { IsDirectlyConnected: true };

    /// <summary>A human-readable explanation of the decision, for diagnostics and simulation events.</summary>
    public string Reason { get; }

    internal static RouteLookupResult Matched(IPv4Address destination, Route route, RouteResolution resolution) =>
        new(hasRoute: true, isResolvable: true, route, resolution, destination,
            $"{destination} matched {route.Destination} ({route.Type.Code()}) - " +
            (route.NextHop is { } nh ? $"next hop {nh}" : "directly connected") +
            $" out {resolution.OutgoingInterface.Name}");

    internal static RouteLookupResult MatchedButUnusable(IPv4Address destination, Route route) =>
        new(hasRoute: true, isResolvable: false, route, resolution: null, destination,
            $"{destination} matched {route.Destination} ({route.Type.Code()}) but it is currently unusable - " +
            (route.NextHop is { } nh ? $"next hop {nh} is unreachable" : "its outgoing interface is down") + ".");

    internal static RouteLookupResult NoRoute(IPv4Address destination) =>
        new(hasRoute: false, isResolvable: false, route: null, resolution: null, destination,
            $"No route to {destination} and no default route.");
}
