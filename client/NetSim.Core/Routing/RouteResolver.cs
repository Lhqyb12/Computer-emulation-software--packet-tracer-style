using NetSim.Core.Networking;

namespace NetSim.Core.Routing;

/// <summary>
/// Turns a matched <see cref="Route"/> into the concrete <see cref="RouteResolution"/> a forwarder
/// needs (Phase 28). The interesting case is a static <em>next-hop</em> route with no exit
/// interface: to forward a packet the router must first know how to reach the next-hop router, so
/// this walks the routing table - next hop after next hop - until it lands on a directly connected
/// (or exit-interface) route, and reports <em>that</em> route's interface together with the
/// <em>original</em> next hop as the address to ARP for.
///
/// <para>Deterministic and loop-safe: every recursion level records the route prefix it went
/// through, so a next-hop cycle (A ג†’ B ג†’ A) resolves to "unusable" instead of spinning, and there
/// is a hard depth cap on top (brief sections 10, 27).</para>
/// </summary>
public static class RouteResolver
{
    /// <summary>Hard cap on recursive next-hop lookups for one resolution - simulator loop safety.</summary>
    public const int MaxRecursionDepth = 8;

    /// <summary>
    /// Resolves <paramref name="route"/> (already selected as the best match for
    /// <paramref name="finalDestination"/>) against <paramref name="allRoutes"/>. Returns
    /// <c>null</c> when the route cannot currently be used - its exit interface is down, its
    /// next hop is unreachable, or a next-hop cycle was detected.
    /// </summary>
    public static RouteResolution? Resolve(
        IEnumerable<Route> allRoutes, Route route, IPv4Address finalDestination)
    {
        ArgumentNullException.ThrowIfNull(allRoutes);
        ArgumentNullException.ThrowIfNull(route);

        var candidates = allRoutes as IReadOnlyCollection<Route> ?? allRoutes.ToList();
        var matched = route;
        return ResolveCore(candidates, route, finalDestination, matched, new HashSet<IPv4Network>(), depth: 0);
    }

    private static RouteResolution? ResolveCore(
        IReadOnlyCollection<Route> allRoutes,
        Route route,
        IPv4Address addressToReach,
        Route matchedRoute,
        HashSet<IPv4Network> visited,
        int depth)
    {
        if (depth > MaxRecursionDepth || !visited.Add(route.Destination))
        {
            return null;
        }

        // A directly connected (or static exit-interface) route: the packet goes straight out this
        // interface, and ARP resolves whatever address we are trying to reach on that link.
        if (route.IsDirectlyConnected)
        {
            return route.OutgoingInterface is { IsOperational: true } egress
                ? new RouteResolution(matchedRoute, egress, addressToReach)
                : null;
        }

        var nextHop = route.NextHop!.Value;

        // A fully specified static route (next hop + exit interface): trust the configured
        // interface, ARP for the stated next hop.
        if (route.OutgoingInterface is { } explicitEgress)
        {
            return explicitEgress.IsOperational
                ? new RouteResolution(matchedRoute, explicitEgress, nextHop)
                : null;
        }

        // A recursive next-hop route: find how to reach the next-hop router, then use that route's
        // interface - but keep ARPing for this level's next hop.
        var viaRoute = LongestPrefixMatch.Select(allRoutes, nextHop);
        return viaRoute is null
            ? null
            : ResolveCore(allRoutes, viaRoute, nextHop, matchedRoute, visited, depth + 1);
    }
}
