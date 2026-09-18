namespace NetSim.Core.Routing;

/// <summary>
/// The default administrative distance for each <see cref="RouteType"/> - the Cisco-style
/// "trustworthiness" ranking a router uses to choose between two routes to the same prefix that
/// were learned different ways (lower wins). Phase 27 never has two sources for one prefix, so this
/// only matters as the Phase 28/29 foundation; it is exposed now so <see cref="Route"/> can carry a
/// sensible default and the <see cref="RoutingTable"/> tie-break is already correct.
/// </summary>
public static class RouteTypeExtensions
{
    /// <summary>The conventional default administrative distance for <paramref name="routeType"/>.</summary>
    public static int DefaultAdministrativeDistance(this RouteType routeType) => routeType switch
    {
        RouteType.Connected => 0,
        RouteType.Static => 1,
        RouteType.Ospf => 110,
        RouteType.Rip => 120,
        RouteType.Default => 1,
        _ => 255,
    };

    /// <summary>A one-letter code for diagnostics output, mirroring a router's routing-table legend (<c>C</c>, <c>S</c>, ...).</summary>
    public static string Code(this RouteType routeType) => routeType switch
    {
        RouteType.Connected => "C",
        RouteType.Static => "S",
        RouteType.Rip => "R",
        RouteType.Ospf => "O",
        RouteType.Default => "S*",
        _ => "?",
    };
}
