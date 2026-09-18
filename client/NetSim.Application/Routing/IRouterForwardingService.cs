using NetSim.Core.Devices;
using NetSim.Core.IP;
using NetSim.Core.Networking;
using NetSim.Core.Topology;

namespace NetSim.Application.Routing;

/// <summary>
/// Orchestrates a single Layer 3 hop through a router inside the simulator: it takes the Core
/// <see cref="Core.Routing.IRoutingEngine"/> decision and carries it out on the wire - resolving
/// the next hop's MAC through the existing ARP engine (broadcasting a request only on a cache
/// miss), building the new Ethernet frame with the <em>router's outgoing-interface</em> MAC as the
/// source, and transmitting it across the outgoing Layer 2 segment (through any switches).
///
/// It is the routing counterpart to how <see cref="Diagnostics.PingService"/> already drives ARP +
/// ICMP + Ethernet by hand - the same layers, no shortcuts. It does one hop; a caller that needs
/// a full path (e.g. the ping tool) calls it once per router on the way.
/// </summary>
public interface IRouterForwardingService
{
    /// <summary>
    /// Routes <paramref name="packet"/>, which arrived on <paramref name="ingressInterface"/> at
    /// <paramref name="router"/>, and - when the decision is to forward - transmits it toward its
    /// next hop across <paramref name="topology"/>. Never throws for an expected condition; every
    /// outcome (local delivery, no route, TTL expiry, ARP failure, a dead link) is a
    /// <see cref="RouterForwardingResult"/>.
    /// </summary>
    RouterForwardingResult ForwardOneHop(
        ITopologyView topology,
        Router router,
        NetworkInterface ingressInterface,
        IPv4Packet packet);
}
