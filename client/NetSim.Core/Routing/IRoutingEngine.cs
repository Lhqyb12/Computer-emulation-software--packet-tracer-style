using NetSim.Core.Devices;
using NetSim.Core.IP;
using NetSim.Core.Networking;

namespace NetSim.Core.Routing;

/// <summary>
/// The Layer 3 forwarding decision maker. Given a <see cref="Router"/>, the interface a packet
/// arrived on and the decapsulated <see cref="IPv4Packet"/>, it decides whether the packet is for
/// the router itself (local delivery), should be forwarded (and out which interface, to which next
/// hop, with the TTL decremented), or must be dropped (no route, TTL expired, ...). It reads the
/// router's <see cref="Router.RoutingTable"/> and nothing else - it does <em>no</em> ARP, builds
/// <em>no</em> Ethernet frame and moves <em>no</em> packet across a cable. Those are the
/// application-layer forwarding orchestrator's job, exactly as
/// <see cref="Icmp.IIcmpLayer"/> stays out of transmission.
///
/// It is the routing counterpart to <see cref="Switching.ISwitchingEngine"/>: switching decides on
/// the destination <em>MAC</em>, routing decides on the destination <em>IP</em>. Deterministic -
/// the same table + packet always produces the same result.
/// </summary>
public interface IRoutingEngine
{
    /// <summary>
    /// Makes the forwarding decision for <paramref name="packet"/> arriving on
    /// <paramref name="ingressInterface"/> at <paramref name="router"/>. Never throws for an
    /// expected condition (invalid packet, interface down, no route, TTL expiry) - each is a
    /// <see cref="RoutingResult"/> outcome. The router's connected routes are re-derived from its
    /// current interface configuration first, so the decision always reflects live interface state.
    /// </summary>
    RoutingResult Route(Router router, NetworkInterface ingressInterface, IPv4Packet packet);

    /// <summary>Raised when a packet is accepted for routing on an interface (before the decision).</summary>
    event EventHandler<RoutingEventArgs>? PacketReceived;

    /// <summary>Raised when the packet is destined for the router itself.</summary>
    event EventHandler<RoutingEventArgs>? LocalDelivery;

    /// <summary>Raised after the routing table lookup succeeds, carrying the selected route.</summary>
    event EventHandler<RoutingEventArgs>? RouteSelected;

    /// <summary>Raised when the packet is forwarded (TTL already decremented on the result's forwarded packet).</summary>
    event EventHandler<RoutingEventArgs>? PacketForwarded;

    /// <summary>Raised when no route (and no default route) matches the destination.</summary>
    event EventHandler<RoutingEventArgs>? NoRouteToDestination;

    /// <summary>Raised when the TTL would expire in transit.</summary>
    event EventHandler<RoutingEventArgs>? TimeToLiveExpired;

    /// <summary>Raised when a packet is dropped for any other reason (invalid packet, interface down, broadcast).</summary>
    event EventHandler<RoutingEventArgs>? PacketDropped;
}
