using NetSim.Core.Icmp;
using NetSim.Core.IP;
using NetSim.Core.Networking;
using NetSim.Core.Routing;
using NetSim.Core.Switching;

namespace NetSim.Application.Routing;

/// <summary>
/// The result of routing one packet through one router (<see cref="IRouterForwardingService.ForwardOneHop"/>):
/// the Core <see cref="Routing"/> decision, and - when the packet was actually put back on the wire -
/// the segment delivery to its next hop so the caller can carry on to the following hop.
/// </summary>
public sealed class RouterForwardingResult
{
    private RouterForwardingResult(
        RouterForwardingOutcome outcome,
        RoutingResult routing,
        NetworkInterface? egressInterface,
        MacAddress? nextHopMac,
        EndpointDelivery? delivery,
        IcmpMessage? generatedIcmpError,
        IPv4Packet? generatedIcmpPacket,
        string detail)
    {
        Outcome = outcome;
        Routing = routing;
        EgressInterface = egressInterface;
        NextHopMac = nextHopMac;
        Delivery = delivery;
        GeneratedIcmpError = generatedIcmpError;
        GeneratedIcmpPacket = generatedIcmpPacket;
        Detail = detail;
    }

    public RouterForwardingOutcome Outcome { get; }

    public bool IsForwarded => Outcome == RouterForwardingOutcome.Forwarded;

    /// <summary>The underlying Layer 3 routing decision (always present).</summary>
    public RoutingResult Routing { get; }

    /// <summary>The interface the packet was forwarded out of - set only when <see cref="IsForwarded"/>.</summary>
    public NetworkInterface? EgressInterface { get; }

    /// <summary>The resolved next-hop MAC address - set only when <see cref="IsForwarded"/>.</summary>
    public MacAddress? NextHopMac { get; }

    /// <summary>
    /// Where the forwarded frame landed at the edge of the outgoing Layer 2 segment - the next
    /// hop's interface, the delivered frame and the tracked packet. Set only when
    /// <see cref="IsForwarded"/>.
    /// </summary>
    public EndpointDelivery? Delivery { get; }

    /// <summary>The ICMP error this router generated (Time Exceeded / Destination Unreachable), if any.</summary>
    public IcmpMessage? GeneratedIcmpError { get; }

    /// <summary>The IPv4 packet carrying <see cref="GeneratedIcmpError"/> back toward the original source, if any.</summary>
    public IPv4Packet? GeneratedIcmpPacket { get; }

    public string Detail { get; }

    internal static RouterForwardingResult Forwarded(
        RoutingResult routing, NetworkInterface egress, MacAddress nextHopMac, EndpointDelivery delivery) =>
        new(RouterForwardingOutcome.Forwarded, routing, egress, nextHopMac, delivery, null, null,
            $"Routed out {egress.Name} to next hop {routing.NextHop} ({nextHopMac}).");

    internal static RouterForwardingResult LocalDelivery(RoutingResult routing) =>
        new(RouterForwardingOutcome.LocalDelivery, routing, null, null, null, null, null, routing.Reason);

    internal static RouterForwardingResult NoRoute(RoutingResult routing, IcmpMessage? icmp, IPv4Packet? icmpPacket) =>
        new(RouterForwardingOutcome.NoRoute, routing, null, null, null, icmp, icmpPacket, routing.Reason);

    internal static RouterForwardingResult TtlExpired(RoutingResult routing, IcmpMessage? icmp, IPv4Packet? icmpPacket) =>
        new(RouterForwardingOutcome.TtlExpired, routing, null, null, null, icmp, icmpPacket, routing.Reason);

    internal static RouterForwardingResult ArpFailed(RoutingResult routing, NetworkInterface egress, string detail) =>
        new(RouterForwardingOutcome.ArpFailed, routing, egress, null, null, null, null, detail);

    internal static RouterForwardingResult InterfaceDown(RoutingResult routing) =>
        new(RouterForwardingOutcome.InterfaceDown, routing, null, null, null, null, null, routing.Reason);

    internal static RouterForwardingResult Dropped(RoutingResult routing, string detail) =>
        new(RouterForwardingOutcome.Dropped, routing, null, null, null, null, null, detail);
}
