namespace NetSim.Application.Routing;

/// <summary>
/// The raw fields a user supplies to add or edit a static route (Phase 28), straight from the UI -
/// all strings / optionals, validated by <see cref="IStaticRouteService"/> before anything touches
/// a routing table. A <c>/0</c> destination is a default static route; at least one of
/// <see cref="NextHop"/> / <see cref="OutgoingInterfaceName"/> must be given.
/// </summary>
public sealed record StaticRouteInput
{
    /// <summary>The destination network address in dotted-decimal form, e.g. <c>192.168.2.0</c> (or <c>0.0.0.0</c> for a default route).</summary>
    public required string DestinationNetwork { get; init; }

    /// <summary>The CIDR prefix length, 0-32.</summary>
    public required int PrefixLength { get; init; }

    /// <summary>The next-hop router's IPv4 address, or <c>null</c>/blank for an exit-interface-only route.</summary>
    public string? NextHop { get; init; }

    /// <summary>The outgoing interface's name (or short name), or <c>null</c>/blank for a next-hop-only route.</summary>
    public string? OutgoingInterfaceName { get; init; }

    /// <summary>Optional route metric (cost). Defaults to 0.</summary>
    public int? Metric { get; init; }

    /// <summary>Optional administrative distance override. Defaults to the route type's conventional value (static = 1).</summary>
    public int? AdministrativeDistance { get; init; }
}
