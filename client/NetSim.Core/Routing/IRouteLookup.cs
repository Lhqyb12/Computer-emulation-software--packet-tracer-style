using NetSim.Core.Networking;

namespace NetSim.Core.Routing;

/// <summary>
/// The read-only face of a routing table: enumerate its routes and resolve a destination address
/// to the best matching one. Separated from <see cref="IRoutingTable"/> so a component that only
/// needs to <em>ask</em> the table (the <see cref="IRoutingEngine"/>, diagnostics, a future
/// packet inspector) depends on the query surface, not the mutation surface.
/// </summary>
public interface IRouteLookup
{
    /// <summary>Every route currently in the table, in a deterministic display order (see <see cref="IRoutingTable"/>).</summary>
    IReadOnlyList<Route> GetRoutes();

    /// <summary>
    /// Resolves <paramref name="destination"/> to the best matching <em>active</em> route using
    /// longest-prefix match (then administrative distance / metric / a stable tie-break), or a
    /// "no route" result when nothing matches and there is no default route.
    /// </summary>
    RouteLookupResult FindBestRoute(IPv4Address destination);
}
