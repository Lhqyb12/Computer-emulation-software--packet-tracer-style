using NetSim.Core.Networking;

namespace NetSim.Core.Routing;

/// <summary>
/// One entry in a <see cref="RoutingTable"/>: "to reach <see cref="Destination"/>, send the packet
/// out <see cref="OutgoingInterface"/>, either straight to the destination host (a directly
/// connected route - <see cref="NextHop"/> is <c>null</c>) or via the router at
/// <see cref="NextHop"/>".
///
/// The type is immutable. In Phase 27 every route is a <see cref="RouteType.Connected"/> route
/// derived from an interface's IPv4 configuration (see <see cref="Connected"/>); the general
/// <see cref="Create"/> factory and the <see cref="Metric"/> / <see cref="AdministrativeDistance"/>
/// fields exist so Phase 28 (static) and Phase 29 (dynamic) can add routes without changing this
/// model or the <see cref="RoutingTable"/> selection algorithm.
///
/// <para><b>Active vs configured.</b> A route is <em>configured</em> as soon as it is in the table,
/// but only <em>usable</em> (<see cref="IsActive"/>) while its <see cref="OutgoingInterface"/> is
/// operational - an interface going down makes its connected route inactive without deleting it
/// (brief section 11).</para>
/// </summary>
public sealed class Route
{
    private Route(
        IPv4Network destination,
        RouteType type,
        NetworkInterface? outgoingInterface,
        IPv4Address? nextHop,
        int metric,
        int administrativeDistance,
        Guid id)
    {
        Destination = destination;
        Type = type;
        OutgoingInterface = outgoingInterface;
        NextHop = nextHop;
        Metric = metric;
        AdministrativeDistance = administrativeDistance;
        Id = id;
    }

    /// <summary>
    /// A stable identity for a manually-configured route (Phase 28), so the UI, the static-route
    /// service and persistence can refer to <em>this</em> route across an edit that changes its
    /// destination prefix. <see cref="Guid.Empty"/> for a derived <see cref="RouteType.Connected"/>
    /// route - those are identified by their prefix and never edited by hand.
    /// </summary>
    public Guid Id { get; }

    /// <summary>The destination network this route matches, e.g. <c>192.168.2.0/24</c> or <c>0.0.0.0/0</c> for a default route.</summary>
    public IPv4Network Destination { get; }

    /// <summary>How the route was learned. Always <see cref="RouteType.Connected"/> in Phase 27.</summary>
    public RouteType Type { get; }

    /// <summary>The interface a matching packet is forwarded out of. Always set for a connected route.</summary>
    public NetworkInterface? OutgoingInterface { get; }

    /// <summary>
    /// The next-hop router's IPv4 address, or <c>null</c> when <see cref="Destination"/> is directly
    /// connected (the packet's own destination is the Layer 2 next hop - ARP resolves it directly).
    /// </summary>
    public IPv4Address? NextHop { get; }

    /// <summary>The route's metric (cost). 0 for a connected route; meaningful only once dynamic routing exists.</summary>
    public int Metric { get; }

    /// <summary>
    /// The administrative distance - how much the router trusts the <em>source</em> of this route
    /// when two routes to the same prefix compete. Lower is better; a connected route is 0.
    /// </summary>
    public int AdministrativeDistance { get; }

    /// <summary>The CIDR prefix length of <see cref="Destination"/> - the primary key for longest-prefix match.</summary>
    public int PrefixLength => Destination.PrefixLength;

    /// <summary>True when this is the default route (<c>0.0.0.0/0</c>).</summary>
    public bool IsDefault => Destination.PrefixLength == 0;

    /// <summary>True when <see cref="NextHop"/> is <c>null</c> - the destination network is on the link out <see cref="OutgoingInterface"/>.</summary>
    public bool IsDirectlyConnected => NextHop is null;

    /// <summary>
    /// True when the route can currently be used: it has no outgoing interface constraint, or that
    /// interface is administratively enabled and operationally up. See the type remarks.
    /// </summary>
    public bool IsActive => OutgoingInterface is null || OutgoingInterface.IsOperational;

    /// <summary>
    /// Builds the directly-connected route implied by an interface carrying
    /// <paramref name="connectedNetwork"/> (the network derived from its address + prefix). No
    /// next hop - the destination host is reached directly on <paramref name="outgoingInterface"/>.
    /// </summary>
    public static Route Connected(NetworkInterface outgoingInterface, IPv4Network connectedNetwork)
    {
        ArgumentNullException.ThrowIfNull(outgoingInterface);
        ArgumentNullException.ThrowIfNull(connectedNetwork);
        return new Route(
            connectedNetwork, RouteType.Connected, outgoingInterface, nextHop: null,
            metric: 0, administrativeDistance: RouteType.Connected.DefaultAdministrativeDistance(),
            id: Guid.Empty);
    }

    /// <summary>
    /// The general-purpose factory Phase 28 (static routing) and Phase 29 (dynamic routing) use.
    /// Phase 27 does not call it, but the routing table and lookup are written against it so those
    /// phases only add callers. <paramref name="id"/> lets a caller preserve a route's identity
    /// across an edit (see <see cref="Id"/>); when omitted a fresh one is generated for a
    /// manually-configured route type and <see cref="Guid.Empty"/> is used for a connected route.
    /// </summary>
    public static Route Create(
        IPv4Network destination,
        RouteType type,
        NetworkInterface? outgoingInterface = null,
        IPv4Address? nextHop = null,
        int metric = 0,
        int? administrativeDistance = null,
        Guid? id = null)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return new Route(
            destination, type, outgoingInterface, nextHop, metric,
            administrativeDistance ?? type.DefaultAdministrativeDistance(),
            id ?? (type == RouteType.Connected ? Guid.Empty : Guid.NewGuid()));
    }

    /// <summary>True when <paramref name="address"/> falls inside this route's destination network.</summary>
    public bool Matches(IPv4Address address) => Destination.Contains(address);

    public override string ToString()
    {
        var via = NextHop is { } nh ? $"via {nh}" : "is directly connected";
        var iface = OutgoingInterface is { } i ? $", {i.Name}" : string.Empty;
        return $"{Type.Code()} {Destination} {via}{iface}";
    }
}
