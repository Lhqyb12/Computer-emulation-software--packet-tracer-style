using System.Linq;
using NetSim.Core.Common.Exceptions;
using NetSim.Core.Networking;

namespace NetSim.Core.Routing;

/// <summary>
/// Default <see cref="IRoutingTable"/>. Backed by a plain list keyed by destination prefix - one
/// route per <see cref="IPv4Network"/> - which is more than fast enough for a simulated lab
/// topology (brief section 64: a straightforward scan is acceptable; the interface hides it so a
/// trie can replace it later). Selection is delegated to <see cref="LongestPrefixMatch"/> so the
/// algorithm stays testable on its own.
///
/// <para>Runtime state, never persisted: a router's connected routes are rebuilt from its
/// (persisted) interface configuration on load - see <c>Router.SyncConnectedRoutes</c> and
/// docs/architecture/routing-engine.md.</para>
/// </summary>
public sealed class RoutingTable : IRoutingTable
{
    // Keyed by destination network so "one route per prefix" is structural. Insertion order is not
    // relied on for correctness - GetRoutes sorts on read, selection sorts in LongestPrefixMatch.
    private readonly Dictionary<IPv4Network, Route> _routes = [];

    public event EventHandler? RoutesChanged;

    public IReadOnlyList<Route> GetRoutes() =>
        _routes.Values
            .OrderBy(r => r.Destination.NetworkAddress)
            .ThenBy(r => r.Destination.PrefixLength)
            .ThenBy(r => r.Type)
            .ToList();

    public IReadOnlyList<Route> GetMatchingRoutes(IPv4Address destination) =>
        _routes.Values
            .Where(r => r.IsActive && r.Matches(destination))
            .OrderByDescending(r => r.PrefixLength)
            .ThenBy(r => r.AdministrativeDistance)
            .ThenBy(r => r.Metric)
            .ThenBy(r => r.Type)
            .ToList();

    public IReadOnlyList<Route> GetRoutesForInterface(NetworkInterface outgoingInterface)
    {
        ArgumentNullException.ThrowIfNull(outgoingInterface);
        return _routes.Values
            .Where(r => r.OutgoingInterface is { } i && i.Id == outgoingInterface.Id)
            .OrderBy(r => r.Destination.NetworkAddress)
            .ThenBy(r => r.Destination.PrefixLength)
            .ToList();
    }

    public RouteLookupResult FindBestRoute(IPv4Address destination)
    {
        // Candidates are already active-filtered and ranked (longest prefix, then AD / metric).
        // Walk them best-first and return the first that actually resolves to a usable interface
        // and next hop - so a static route whose transit path is down transparently yields to a
        // less specific route that still works (brief sections 16, 31).
        var ranked = GetMatchingRoutes(destination);
        if (ranked.Count == 0)
        {
            return RouteLookupResult.NoRoute(destination);
        }

        foreach (var candidate in ranked)
        {
            var resolution = RouteResolver.Resolve(_routes.Values, candidate, destination);
            if (resolution is not null)
            {
                return RouteLookupResult.Matched(destination, candidate, resolution);
            }
        }

        // Something matched but nothing is usable right now - keep the best match so diagnostics
        // and the UI can say "route configured, currently unusable" rather than "no route".
        return RouteLookupResult.MatchedButUnusable(destination, ranked[0]);
    }

    public void AddRoute(Route route)
    {
        ArgumentNullException.ThrowIfNull(route);
        Validate(route);

        if (route.Type != RouteType.Connected
            && _routes.TryGetValue(route.Destination, out var existing)
            && existing.Type == RouteType.Connected)
        {
            throw new DomainException(
                $"{route.Destination} is a directly connected network - it cannot be overridden by a {route.Type} route.");
        }

        _routes[route.Destination] = route;
        RoutesChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool RemoveRoute(IPv4Network destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (!_routes.Remove(destination))
        {
            return false;
        }

        RoutesChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void ReplaceConnectedRoutes(IEnumerable<Route> connectedRoutes)
    {
        ArgumentNullException.ThrowIfNull(connectedRoutes);

        var replacement = connectedRoutes.ToList();
        foreach (var route in replacement)
        {
            if (route.Type != RouteType.Connected)
            {
                throw new DomainException($"ReplaceConnectedRoutes only accepts connected routes, but got a {route.Type} route.");
            }

            Validate(route);
        }

        // Drop every existing connected route, then add the new set. Non-connected routes (Phase 28+)
        // are deliberately untouched.
        foreach (var prefix in _routes.Where(kv => kv.Value.Type == RouteType.Connected).Select(kv => kv.Key).ToList())
        {
            _routes.Remove(prefix);
        }

        foreach (var route in replacement)
        {
            _routes[route.Destination] = route;
        }

        RoutesChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        if (_routes.Count == 0)
        {
            return;
        }

        _routes.Clear();
        RoutesChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void Validate(Route route)
    {
        if (route.Type == RouteType.Connected)
        {
            if (route.OutgoingInterface is null)
            {
                throw new DomainException("A connected route must have an outgoing interface.");
            }

            if (route.NextHop is not null)
            {
                throw new DomainException("A connected route cannot have a next hop - its destination network is directly connected.");
            }

            return;
        }

        // Non-connected (Phase 28+) routes: at least one of an outgoing interface / a next hop, and
        // a next hop that could plausibly be a router address.
        if (route.OutgoingInterface is null && route.NextHop is null)
        {
            throw new DomainException("A route must have an outgoing interface, a next hop, or both.");
        }

        if (route.NextHop is { } nextHop &&
            (nextHop.IsUnspecified || nextHop.IsLimitedBroadcast || nextHop.IsMulticast))
        {
            throw new DomainException($"'{nextHop}' is not a valid next-hop address.");
        }
    }
}
