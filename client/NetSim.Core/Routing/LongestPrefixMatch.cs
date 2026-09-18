using NetSim.Core.Networking;

namespace NetSim.Core.Routing;

/// <summary>
/// The pure route-selection algorithm, isolated so it can be tested on its own and swapped for a
/// trie/radix implementation later without touching callers (brief sections 12, 60, 64). Given a
/// set of candidate routes and a destination address it applies, in order:
/// <list type="number">
/// <item>keep only <em>active</em> routes whose network <see cref="IPv4Network.Contains(IPv4Address)"/> the destination (a <c>0.0.0.0/0</c> default route always contains it);</item>
/// <item>prefer the longest prefix - <c>10.10.20.0/24</c> beats <c>10.0.0.0/8</c> beats <c>0.0.0.0/0</c>;</item>
/// <item>then the lowest administrative distance, then the lowest metric (the Phase 28/29 tie-breaks);</item>
/// <item>then a stable deterministic order (route type, then the interface name) so the result never depends on insertion order.</item>
/// </list>
/// Correctness never relies on the input being pre-sorted.
/// </summary>
public static class LongestPrefixMatch
{
    /// <summary>
    /// Returns the single best route for <paramref name="destination"/> from
    /// <paramref name="candidates"/>, or <c>null</c> when none match (and there is no default route).
    /// </summary>
    public static Route? Select(IEnumerable<Route> candidates, IPv4Address destination)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        Route? best = null;
        foreach (var route in candidates)
        {
            if (!route.IsActive || !route.Matches(destination))
            {
                continue;
            }

            if (best is null || IsBetter(route, best))
            {
                best = route;
            }
        }

        return best;
    }

    // True when "candidate" should win over "incumbent" under the ordering described in the remarks.
    private static bool IsBetter(Route candidate, Route incumbent)
    {
        if (candidate.PrefixLength != incumbent.PrefixLength)
        {
            return candidate.PrefixLength > incumbent.PrefixLength;
        }

        if (candidate.AdministrativeDistance != incumbent.AdministrativeDistance)
        {
            return candidate.AdministrativeDistance < incumbent.AdministrativeDistance;
        }

        if (candidate.Metric != incumbent.Metric)
        {
            return candidate.Metric < incumbent.Metric;
        }

        if (candidate.Type != incumbent.Type)
        {
            return candidate.Type < incumbent.Type;
        }

        // Fully deterministic final tie-break so two otherwise-identical routes order stably.
        return string.CompareOrdinal(
            candidate.OutgoingInterface?.Name ?? string.Empty,
            incumbent.OutgoingInterface?.Name ?? string.Empty) < 0;
    }
}
