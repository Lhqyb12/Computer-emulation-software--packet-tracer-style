using NetSim.Core.Networking;

namespace NetSim.Core.Routing;

/// <summary>
/// A router's IPv4 routing table: the set of <see cref="Route"/>s it consults to pick an outgoing
/// interface and next hop for a destination address. Extends <see cref="IRouteLookup"/> with the
/// controlled mutation operations.
///
/// <para>In Phase 27 the only routes are <see cref="RouteType.Connected"/> ones, and they are
/// <em>derived</em> from the router's interface configuration rather than added by hand - see
/// <see cref="ReplaceConnectedRoutes"/> and <c>Router.SyncConnectedRoutes</c>. The
/// <see cref="AddRoute"/> / <see cref="RemoveRoute"/> pair is the foundation Phase 28 static
/// routing builds on; it validates its input rather than trusting callers.</para>
/// </summary>
public interface IRoutingTable : IRouteLookup
{
    /// <summary>Raised after any change to the set of routes (add, remove, clear, connected-route resync).</summary>
    event EventHandler? RoutesChanged;

    /// <summary>The routes matching a given destination address, longest-prefix first - the full ranked candidate list behind <see cref="IRouteLookup.FindBestRoute"/>.</summary>
    IReadOnlyList<Route> GetMatchingRoutes(IPv4Address destination);

    /// <summary>Every route whose <see cref="Route.OutgoingInterface"/> is <paramref name="outgoingInterface"/>.</summary>
    IReadOnlyList<Route> GetRoutesForInterface(NetworkInterface outgoingInterface);

    /// <summary>
    /// Adds <paramref name="route"/>. Throws <see cref="Common.Exceptions.DomainException"/> for an
    /// invalid route (a non-connected route with neither an outgoing interface nor a next hop, a
    /// next hop that is not a plausible host address, ...). A connected route for a prefix that is
    /// already present replaces the existing one.
    /// </summary>
    void AddRoute(Route route);

    /// <summary>Removes the route for exactly <paramref name="destination"/> (network + prefix). Returns false when none was present.</summary>
    bool RemoveRoute(IPv4Network destination);

    /// <summary>
    /// Replaces every <see cref="RouteType.Connected"/> route with <paramref name="connectedRoutes"/>
    /// in one atomic step (non-connected routes are left untouched). This is how a router keeps its
    /// connected routes in sync with its interfaces without ever leaving a stale one behind.
    /// </summary>
    void ReplaceConnectedRoutes(IEnumerable<Route> connectedRoutes);

    /// <summary>Removes every route.</summary>
    void Clear();
}
