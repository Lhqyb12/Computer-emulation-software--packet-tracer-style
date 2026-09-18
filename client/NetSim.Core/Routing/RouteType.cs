namespace NetSim.Core.Routing;

/// <summary>
/// How a <see cref="Route"/> came to be in a routing table. Phase 27 only ever populates
/// <see cref="Connected"/> routes (automatically derived from a router interface's IPv4
/// configuration); the other members exist so the route model and the
/// <see cref="RoutingTable"/> selection algorithm are ready for Phase 28 (static routing) and
/// Phase 29 (dynamic routing) without a redesign. Nothing in this phase creates a
/// <see cref="Static"/>, <see cref="Rip"/>, <see cref="Ospf"/> or <see cref="Default"/> route.
/// </summary>
public enum RouteType
{
    /// <summary>Directly connected network - derived from an interface's own IPv4 address/prefix. The only type Phase 27 creates.</summary>
    Connected,

    /// <summary>An administrator-configured static route (Phase 28).</summary>
    Static,

    /// <summary>Learned from the RIP routing protocol (Phase 29).</summary>
    Rip,

    /// <summary>Learned from the OSPF routing protocol (Phase 29).</summary>
    Ospf,

    /// <summary>An explicitly configured default route (0.0.0.0/0). Represented for Phase 28; not created here.</summary>
    Default,
}
