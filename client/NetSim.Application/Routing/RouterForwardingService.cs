using System.Linq;
using NetSim.Core.Arp;
using NetSim.Core.Devices;
using NetSim.Core.Ethernet;
using NetSim.Core.Icmp;
using NetSim.Core.IP;
using NetSim.Core.Networking;
using NetSim.Core.Routing;
using NetSim.Core.Switching;
using NetSim.Core.Topology;

namespace NetSim.Application.Routing;

/// <summary>
/// Default <see cref="IRouterForwardingService"/>. See the interface for the responsibility.
/// Every engine it calls is the real Phase 17-27 one - <see cref="IRoutingEngine"/> for the
/// decision, <see cref="IArpLayer"/> for next-hop resolution, <see cref="IIPv4Layer"/> /
/// <see cref="ISwitchedSegmentService"/> for encapsulation and transmission, <see cref="IIcmpLayer"/>
/// for the error messages a router owes the source on a drop.
/// </summary>
public sealed class RouterForwardingService : IRouterForwardingService
{
    private readonly IRoutingEngine _routingEngine;
    private readonly IArpLayer _arp;
    private readonly IIPv4Layer _ipv4;
    private readonly IIcmpLayer _icmp;
    private readonly ISwitchedSegmentService _segment;

    public RouterForwardingService(
        IRoutingEngine routingEngine,
        IArpLayer arp,
        IIPv4Layer ipv4,
        IIcmpLayer icmp,
        ISwitchedSegmentService segment)
    {
        ArgumentNullException.ThrowIfNull(routingEngine);
        ArgumentNullException.ThrowIfNull(arp);
        ArgumentNullException.ThrowIfNull(ipv4);
        ArgumentNullException.ThrowIfNull(icmp);
        ArgumentNullException.ThrowIfNull(segment);

        _routingEngine = routingEngine;
        _arp = arp;
        _ipv4 = ipv4;
        _icmp = icmp;
        _segment = segment;
    }

    public RouterForwardingResult ForwardOneHop(
        ITopologyView topology,
        Router router,
        NetworkInterface ingressInterface,
        IPv4Packet packet)
    {
        ArgumentNullException.ThrowIfNull(topology);
        ArgumentNullException.ThrowIfNull(router);
        ArgumentNullException.ThrowIfNull(ingressInterface);
        ArgumentNullException.ThrowIfNull(packet);

        var routing = _routingEngine.Route(router, ingressInterface, packet);

        switch (routing.Outcome)
        {
            case RoutingOutcome.LocalDelivery:
                return RouterForwardingResult.LocalDelivery(routing);

            case RoutingOutcome.InterfaceDown:
                return RouterForwardingResult.InterfaceDown(routing);

            case RoutingOutcome.NoRoute:
            {
                var (icmp, icmpPacket) = BuildIcmpError(router, ingressInterface, packet, routing);
                return RouterForwardingResult.NoRoute(routing, icmp, icmpPacket);
            }

            case RoutingOutcome.TtlExpired:
            {
                var (icmp, icmpPacket) = BuildIcmpError(router, ingressInterface, packet, routing);
                return RouterForwardingResult.TtlExpired(routing, icmp, icmpPacket);
            }

            case RoutingOutcome.Forward:
                return Transmit(topology, routing);

            default:
                return RouterForwardingResult.Dropped(routing, routing.Reason);
        }
    }

    private RouterForwardingResult Transmit(ITopologyView topology, RoutingResult routing)
    {
        var egress = routing.OutgoingInterface!;
        var forwardedPacket = routing.ForwardedPacket!;
        var nextHopIp = routing.NextHop!.Value;

        if (egress.MacAddress is not { } egressMac)
        {
            return RouterForwardingResult.Dropped(routing, $"Outgoing interface '{egress.Name}' has no MAC address.");
        }

        if (!egress.IsOperational)
        {
            return RouterForwardingResult.Dropped(routing, $"Outgoing interface '{egress.Name}' is not operational.");
        }

        var nextHopMac = ResolveMac(topology, egress, nextHopIp, out var arpFailure);
        if (nextHopMac is null)
        {
            return RouterForwardingResult.ArpFailed(routing, egress, arpFailure ?? $"ARP could not resolve {nextHopIp}.");
        }

        var frame = _ipv4.Encapsulate(forwardedPacket, egressMac, nextHopMac.Value);
        var segment = _segment.TransmitAcrossSegment(topology, egress, frame);
        if (segment.Deliveries.Count == 0)
        {
            var detail = segment.FirstHopDetail
                ?? (segment.LoopSafetyTriggered ? segment.LoopSafetyReason : null)
                ?? "The routed frame did not reach any device.";
            return RouterForwardingResult.Dropped(routing, detail);
        }

        var delivery = segment.DeliveryToMac(nextHopMac.Value) ?? segment.Deliveries[0];
        _ipv4.Process(delivery.Packet);
        return RouterForwardingResult.Forwarded(routing, egress, nextHopMac.Value, delivery);
    }

    private (IcmpMessage? Message, IPv4Packet? Packet) BuildIcmpError(
        Router router, NetworkInterface ingressInterface, IPv4Packet offendingPacket, RoutingResult routing)
    {
        // The router answers from the address the packet came in on - the address the source can
        // route a reply back to. If the ingress interface has no IPv4 address, no error is sent.
        if (ingressInterface.PrimaryIPv4Configuration is not { } ingressConfig)
        {
            return (null, null);
        }

        if (routing.SuggestedIcmpError is not { } type || routing.IcmpContext is not { } context)
        {
            return (null, null);
        }

        var message = type == IcmpType.TimeExceeded
            ? _icmp.CreateTimeExceeded(context, routing.SuggestedIcmpCode)
            : _icmp.CreateDestinationUnreachable(context, routing.SuggestedIcmpCode ?? IcmpCode.NetworkUnreachable);

        var icmpPacket = _icmp.Encapsulate(message, ingressConfig.Address, offendingPacket.SourceAddress);
        return (message, icmpPacket);
    }

    /// <summary>
    /// Resolves <paramref name="targetIp"/>'s MAC from <paramref name="fromInterface"/>: a cache hit
    /// returns immediately; a miss drives the broadcast-request / unicast-reply round trip across
    /// the segment, exactly as <see cref="Diagnostics.PingService"/> does. Returns null with
    /// <paramref name="failure"/> set when resolution cannot complete.
    /// </summary>
    private MacAddress? ResolveMac(ITopologyView topology, NetworkInterface fromInterface, IPv4Address targetIp, out string? failure)
    {
        var resolution = _arp.Resolve(fromInterface, targetIp);
        if (resolution.IsResolved)
        {
            failure = null;
            return resolution.HardwareAddress;
        }

        if (resolution.HasFailed)
        {
            failure = resolution.FailureReason ?? "ARP resolution failed.";
            return null;
        }

        var requestSegment = _segment.TransmitAcrossSegment(topology, fromInterface, resolution.RequestFrame!);
        if (requestSegment.Deliveries.Count == 0)
        {
            failure = requestSegment.FirstHopDetail ?? "The ARP request could not be sent.";
            return null;
        }

        NetworkInterface? responder = null;
        ArpProcessingReport? arpReport = null;
        foreach (var delivery in requestSegment.Deliveries)
        {
            var report = _arp.HandleIncoming(delivery.DestinationInterface, delivery.Frame);
            if (report.ReplyGenerated)
            {
                responder = delivery.DestinationInterface;
                arpReport = report;
                break;
            }
        }

        if (responder is null || arpReport is null)
        {
            failure = $"No device on the link answered the ARP request for {targetIp}.";
            return null;
        }

        var replySegment = _segment.TransmitAcrossSegment(topology, responder, arpReport.ReplyFrame!);
        var replyDelivery = replySegment.DeliveryToMac(fromInterface.MacAddress ?? default)
            ?? replySegment.Deliveries.FirstOrDefault();
        if (replyDelivery is null)
        {
            failure = "The ARP reply could not be sent back.";
            return null;
        }

        _arp.HandleIncoming(replyDelivery.DestinationInterface, replyDelivery.Frame);

        if (!fromInterface.ArpCache.TryGet(targetIp, out var entry))
        {
            failure = $"ARP resolution for {targetIp} did not complete.";
            return null;
        }

        failure = null;
        return entry.HardwareAddress;
    }
}
