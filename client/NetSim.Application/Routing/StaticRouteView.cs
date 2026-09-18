using NetSim.Core.Routing;

namespace NetSim.Application.Routing;

/// <summary>
/// A read model of one configured static (or default) route for the UI (Phase 28): its identity,
/// destination, next hop / exit interface, metric / administrative distance, route type and current
/// usability. Projected from a Core <see cref="Route"/> - the routing engine stays out of the UI.
/// </summary>
public sealed class StaticRouteView
{
    internal StaticRouteView(Route route, string? outgoingInterfaceName, StaticRouteStatus status)
    {
        Id = route.Id;
        DestinationNetwork = route.Destination.NetworkAddress.ToString();
        PrefixLength = route.Destination.PrefixLength;
        DestinationCidr = route.Destination.ToString();
        NextHop = route.NextHop?.ToString();
        OutgoingInterfaceName = outgoingInterfaceName;
        Metric = route.Metric;
        AdministrativeDistance = route.AdministrativeDistance;
        IsDefaultRoute = route.IsDefault;
        RouteType = route.Type;
        Status = status;
    }

    public Guid Id { get; }

    public string DestinationNetwork { get; }

    public int PrefixLength { get; }

    public string DestinationCidr { get; }

    public string? NextHop { get; }

    public string? OutgoingInterfaceName { get; }

    public int Metric { get; }

    public int AdministrativeDistance { get; }

    public bool IsDefaultRoute { get; }

    public RouteType RouteType { get; }

    /// <summary>"Default" for a <c>0.0.0.0/0</c> route, otherwise "Static".</summary>
    public string RouteTypeText => IsDefaultRoute ? "Default" : "Static";

    public StaticRouteStatus Status { get; }

    public bool IsActive => Status == StaticRouteStatus.Active;

    public string StatusText => Status switch
    {
        StaticRouteStatus.Active => "Active",
        StaticRouteStatus.InterfaceDown => "Interface Down",
        StaticRouteStatus.UnreachableNextHop => "Unreachable Next Hop",
        _ => "Inactive",
    };
}
