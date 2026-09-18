using System.Linq;
using NetSim.Application.Routing;
using NetSim.Application.Services;
using NetSim.Core.Arp;
using NetSim.Core.Devices;
using NetSim.Core.Ethernet;
using NetSim.Core.Icmp;
using NetSim.Core.IP;
using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Routing;
using NetSim.Core.Switching;
using NetSim.Core.Topology;

namespace NetSim.Application.Diagnostics;

/// <summary>
/// Default <see cref="IPingService"/>. Orchestrates the conceptual flow the brief lays out (section
/// 4): build the Echo Request, resolve the first Layer 2 next hop through ARP (broadcasting a
/// request only on a cache miss), encapsulate through IPv4 and Ethernet, and carry the frame across
/// the network - through any switches (Phase 25) <em>and</em> through any routers on the path
/// (Phase 27): at each router the real <see cref="IRouterForwardingService"/> / Core
/// <see cref="IRoutingEngine"/> makes the Layer 3 forwarding decision (longest-prefix match,
/// TTL decrement, ARP for the next hop, a new Ethernet frame with the router's outgoing-interface
/// MAC). The destination's <see cref="IIcmpLayer"/> decides whether it owns the address and builds
/// the Echo Reply, which is then routed back the same way. Every step calls the real Phase 17-27
/// engines directly - no shortcuts, no fake "success".
///
/// A destination on the source interface's own subnet is delivered directly (no router involved,
/// behaviour unchanged from Phase 21-26). A destination off-subnet needs a
/// <see cref="NetworkInterface.IPv4DefaultGateway"/> on the source interface; without one the
/// result is <see cref="PingReplyStatus.NoRoute"/>, exactly as a real host with no default route.
/// </summary>
public sealed class PingService : IPingService
{
    /// <summary>Default echo payload size in bytes - the classic `ping` default.</summary>
    public const int DefaultPayloadSizeBytes = 32;

    /// <summary>Hard cap on router hops for one direction of one echo - simulator loop safety (brief section 61).</summary>
    private const int MaxRouterHops = 16;

    private readonly IIcmpLayer _icmp;
    private readonly IIPv4Layer _ipv4;
    private readonly IArpLayer _arp;
    private readonly IEthernetTransmissionService _ethernet;
    private readonly ISwitchedSegmentService _segment;
    private readonly IRouterForwardingService _forwarding;
    private readonly INetworkService _networkService;

    public PingService(
        IIcmpLayer icmp, IIPv4Layer ipv4, IArpLayer arp, IEthernetTransmissionService ethernet, INetworkService networkService,
        ISwitchedSegmentService? segment = null, IRouterForwardingService? forwarding = null)
    {
        ArgumentNullException.ThrowIfNull(icmp);
        ArgumentNullException.ThrowIfNull(ipv4);
        ArgumentNullException.ThrowIfNull(arp);
        ArgumentNullException.ThrowIfNull(ethernet);
        ArgumentNullException.ThrowIfNull(networkService);

        _icmp = icmp;
        _ipv4 = ipv4;
        _arp = arp;
        _ethernet = ethernet;
        _segment = segment ?? new SwitchedSegmentService(_ethernet, new SwitchingEngine());
        _forwarding = forwarding ?? new RouterForwardingService(new RoutingEngine(), _arp, _ipv4, _icmp, _segment);
        _networkService = networkService;
    }

    // Sends one frame across the whole Layer 2 segment (through any switches) and picks the
    // delivery to focus on: the one received by the interface owning <paramref name="preferMac"/>
    // if given (the intended unicast target), otherwise the first delivery. On a switchless segment
    // there is always exactly one delivery, so this is equivalent to a direct Ethernet transmit.
    private HopOutcome SendFrame(ITopologyView topology, NetworkInterface source, EthernetFrame frame, MacAddress? preferMac = null)
    {
        var segment = _segment.TransmitAcrossSegment(topology, source, frame);
        if (segment.Deliveries.Count == 0)
        {
            var detail = segment.FirstHopDetail
                ?? (segment.LoopSafetyTriggered ? segment.LoopSafetyReason : null)
                ?? "The frame did not reach any device.";
            return new HopOutcome(false, null, null, null, detail);
        }

        var delivery = (preferMac is { } mac ? segment.DeliveryToMac(mac) : null) ?? segment.Deliveries[0];
        return new HopOutcome(true, delivery.DestinationInterface, delivery.Packet, delivery.Frame, null);
    }

    private readonly record struct HopOutcome(
        bool Success, NetworkInterface? DestinationInterface, Packet? Packet, EthernetFrame? DeliveredFrame, string? Detail);

    // The final landing point of a routed delivery: the interface that received it, the IPv4 packet
    // it carried and the frame it arrived in.
    private readonly record struct RoutedDelivery(NetworkInterface Interface, IPv4Packet IpPacket, EthernetFrame Frame);

    public PingSessionResult Ping(NetworkInterface sourceInterface, IPv4Address destination, int count = 4, RawPayload? payload = null)
    {
        ArgumentNullException.ThrowIfNull(sourceInterface);
        if (count < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Ping count must be at least 1.");
        }

        var data = payload ?? RawPayload.OfSize(DefaultPayloadSizeBytes);
        var identifier = (ushort)Random.Shared.Next(1, ushort.MaxValue);

        var replies = new List<PingReply>(count);
        for (var sequence = 1; sequence <= count; sequence++)
        {
            replies.Add(SendOne(sourceInterface, destination, identifier, sequence, data));
        }

        return PingSessionResult.Create(destination, replies);
    }

    private PingReply SendOne(NetworkInterface sourceInterface, IPv4Address destination, ushort identifier, int sequence, RawPayload data)
    {
        if (destination.IsUnspecified || destination.IsLimitedBroadcast || destination.IsMulticast)
        {
            return PingReply.Failure(PingReplyStatus.InvalidDestination, sequence, destination, $"'{destination}' is not a valid ping destination.");
        }

        if (_networkService.CurrentNetwork is not { } topology)
        {
            return PingReply.Failure(PingReplyStatus.Error, sequence, destination, "No network is currently open.");
        }

        if (sourceInterface.PrimaryIPv4Configuration is not { } sourceConfig)
        {
            return PingReply.Failure(PingReplyStatus.NoSourceAddress, sequence, destination,
                $"No IPv4 address is configured on interface '{sourceInterface.Name}'.");
        }

        // Where does the first Layer 2 hop go? Straight to the destination if it is on this
        // interface's subnet, otherwise to the configured default gateway (brief section 15/47).
        if (!TryResolveFirstHop(sourceInterface, sourceConfig, destination, out var forwardHopIp, out var noRouteReason))
        {
            return PingReply.Failure(PingReplyStatus.NoRoute, sequence, destination, noRouteReason!);
        }

        var forwardHopMac = ResolveMac(topology, sourceInterface, forwardHopIp, sequence, destination, out var forwardFailure);
        if (forwardFailure is not null)
        {
            return forwardFailure;
        }

        var echoRequest = _icmp.CreateEchoRequest(identifier, (ushort)sequence, data);
        var requestIpPacket = _icmp.Encapsulate(echoRequest, sourceConfig.Address, destination);

        var forwardDelivery = DeliverRouted(
            topology, sourceInterface, forwardHopMac!.Value, requestIpPacket, destination, sequence, echoRequest, out var deliveryFailure);
        if (deliveryFailure is not null)
        {
            return deliveryFailure;
        }

        var destinationInterface = forwardDelivery!.Value.Interface;
        var deliveredRequestIpPacket = forwardDelivery.Value.IpPacket;

        var echoReport = _icmp.HandleIncoming(destinationInterface, deliveredRequestIpPacket);
        if (!echoReport.IsSuccess || !echoReport.ReplyGenerated)
        {
            return PingReply.Failure(PingReplyStatus.TimedOut, sequence, destination,
                $"No device at {destination} responded to the echo request.", echoRequest);
        }

        // Return path: from the responder back toward the source. A host responder uses its own
        // subnet or its default gateway; a router responder consults its own routing table.
        if (!TryResolveReturnPath(destinationInterface, sourceConfig.Address, out var replyEgress, out var replyHopIp, out var returnNoRoute))
        {
            return PingReply.Failure(PingReplyStatus.NoRoute, sequence, destination, returnNoRoute!, echoRequest);
        }

        var replyHopMac = ResolveMac(topology, replyEgress, replyHopIp, sequence, destination, out var returnFailure);
        if (returnFailure is not null)
        {
            return returnFailure;
        }

        var replyDelivery = DeliverRouted(
            topology, replyEgress, replyHopMac!.Value, echoReport.ReplyPacket!, sourceConfig.Address, sequence, echoRequest,
            out var replyDeliveryFailure);
        if (replyDeliveryFailure is not null)
        {
            return replyDeliveryFailure;
        }

        var deliveredReplyIpPacket = replyDelivery!.Value.IpPacket;
        var replyReport = _icmp.HandleIncoming(sourceInterface, deliveredReplyIpPacket);
        if (!replyReport.IsSuccess || replyReport.Message is not { IsEchoReply: true } echoReply)
        {
            return PingReply.Failure(PingReplyStatus.Error, sequence, destination,
                "The reply received was not a valid Echo Reply.", echoRequest);
        }

        if (echoReply.Identifier != identifier || echoReply.SequenceNumber != (ushort)sequence)
        {
            return PingReply.Failure(PingReplyStatus.Error, sequence, destination,
                "The Echo Reply's identifier/sequence number did not match the request.", echoRequest);
        }

        // No simulated-clock/latency model exists yet - a nominal, clearly-labelled value stands in
        // (see docs/architecture/icmp-engine.md, "Round-trip time"). Never real wall-clock time.
        var roundTripTime = TimeSpan.FromMilliseconds(1);

        return PingReply.Success(
            sequence, destination, deliveredReplyIpPacket.SourceAddress, deliveredReplyIpPacket.TimeToLive, roundTripTime, echoRequest, echoReply);
    }

    private static bool TryResolveFirstHop(
        NetworkInterface sourceInterface, Ipv4InterfaceConfiguration sourceConfig, IPv4Address destination,
        out IPv4Address firstHop, out string? noRouteReason)
    {
        if (sourceConfig.Network.Contains(destination))
        {
            firstHop = destination;
            noRouteReason = null;
            return true;
        }

        if (sourceInterface.IPv4DefaultGateway is { } gateway)
        {
            firstHop = gateway;
            noRouteReason = null;
            return true;
        }

        firstHop = default;
        noRouteReason =
            $"{destination} is outside {sourceInterface.Name}'s local subnet ({sourceConfig.Network}) and no default gateway is configured.";
        return false;
    }

    /// <summary>
    /// Works out how the responder sends its Echo Reply back toward <paramref name="originalSource"/>:
    /// which interface to leave through (<paramref name="egress"/>) and which IPv4 address ARP must
    /// resolve for the first Layer 2 hop (<paramref name="returnHop"/>). A router responder consults
    /// its own routing table (it needs no default gateway); a host responder uses its local subnet
    /// or its configured default gateway.
    /// </summary>
    private static bool TryResolveReturnPath(
        NetworkInterface responderInterface, IPv4Address originalSource,
        out NetworkInterface egress, out IPv4Address returnHop, out string? noRouteReason)
    {
        egress = responderInterface;
        returnHop = default;
        noRouteReason = null;

        if (responderInterface.Device is Router router)
        {
            router.SyncConnectedRoutes();
            var lookup = router.RoutingTable.FindBestRoute(originalSource);
            if (!lookup.HasRoute)
            {
                noRouteReason = $"The router has no route back to {originalSource}.";
                return false;
            }

            egress = lookup.OutgoingInterface ?? responderInterface;
            returnHop = lookup.NextHopAddress ?? originalSource;
            return true;
        }

        if (responderInterface.PrimaryIPv4Configuration is not { } responderConfig)
        {
            noRouteReason = "The responding interface has no IPv4 address to send the reply from.";
            return false;
        }

        if (responderConfig.Network.Contains(originalSource))
        {
            returnHop = originalSource;
            return true;
        }

        if (responderInterface.IPv4DefaultGateway is { } gateway)
        {
            returnHop = gateway;
            return true;
        }

        noRouteReason =
            $"The echo reply cannot be routed back to {originalSource}: the responder has no default gateway configured.";
        return false;
    }

    /// <summary>
    /// Carries <paramref name="ipPacket"/> from <paramref name="egress"/> toward
    /// <paramref name="finalDestination"/>: one physical delivery to <paramref name="l2NextHopMac"/>,
    /// then - for as long as the frame lands on a router that does not own the final destination -
    /// one <see cref="IRouterForwardingService.ForwardOneHop"/> per router. Returns where the packet
    /// finally landed, or null with <paramref name="failure"/> set.
    /// </summary>
    private RoutedDelivery? DeliverRouted(
        ITopologyView topology, NetworkInterface egress, MacAddress l2NextHopMac, IPv4Packet ipPacket,
        IPv4Address finalDestination, int sequence, IcmpMessage echoRequest, out PingReply? failure)
    {
        failure = null;

        if (egress.MacAddress is not { } egressMac)
        {
            failure = PingReply.Failure(PingReplyStatus.Error, sequence, finalDestination,
                $"Interface '{egress.Name}' has no MAC address to send from.", echoRequest);
            return null;
        }

        var frame = _ipv4.Encapsulate(ipPacket, egressMac, l2NextHopMac);
        var tx = SendFrame(topology, egress, frame, l2NextHopMac);
        if (!tx.Success)
        {
            failure = PingReply.Failure(PingReplyStatus.DestinationUnreachable, sequence, finalDestination,
                tx.Detail ?? "The packet could not be sent.", echoRequest);
            return null;
        }

        var ipProcess = _ipv4.Process(tx.Packet!);
        if (!ipProcess.IsSuccess)
        {
            failure = PingReply.Failure(PingReplyStatus.DestinationUnreachable, sequence, finalDestination,
                ipProcess.Detail ?? "The IPv4 packet was rejected.", echoRequest);
            return null;
        }

        var landedInterface = tx.DestinationInterface!;
        var landedFrame = tx.DeliveredFrame!;

        for (var hop = 0; hop < MaxRouterHops; hop++)
        {
            if (!_ipv4.TryDecapsulate(landedFrame, out var landedIpPacket))
            {
                failure = PingReply.Failure(PingReplyStatus.Error, sequence, finalDestination,
                    "A delivered frame did not carry an IPv4 packet.", echoRequest);
                return null;
            }

            // A non-router endpoint is where the packet is consumed.
            if (landedInterface.Device is not Router router)
            {
                return new RoutedDelivery(landedInterface, landedIpPacket, landedFrame);
            }

            // A router that owns the final destination on one of its interfaces (ping-the-router,
            // possibly on a different interface than the frame arrived on): hand the packet to the
            // owning interface so its ICMP layer answers.
            if (RouterInterfaceOwning(router, finalDestination) is { } owningInterface)
            {
                return new RoutedDelivery(owningInterface, landedIpPacket, landedFrame);
            }

            var forwarded = _forwarding.ForwardOneHop(topology, router, landedInterface, landedIpPacket);
            switch (forwarded.Outcome)
            {
                case RouterForwardingOutcome.Forwarded:
                    var delivery = forwarded.Delivery!;
                    landedInterface = delivery.DestinationInterface;
                    landedFrame = delivery.Frame;
                    continue;

                case RouterForwardingOutcome.LocalDelivery:
                    return new RoutedDelivery(landedInterface, landedIpPacket, landedFrame);

                case RouterForwardingOutcome.NoRoute:
                    failure = PingReply.Failure(PingReplyStatus.NoRoute, sequence, finalDestination, forwarded.Detail, echoRequest);
                    return null;

                case RouterForwardingOutcome.TtlExpired:
                    failure = PingReply.Failure(PingReplyStatus.TtlExpired, sequence, finalDestination, forwarded.Detail, echoRequest);
                    return null;

                case RouterForwardingOutcome.ArpFailed:
                    failure = PingReply.Failure(PingReplyStatus.ArpResolutionFailed, sequence, finalDestination, forwarded.Detail, echoRequest);
                    return null;

                default:
                    failure = PingReply.Failure(PingReplyStatus.DestinationUnreachable, sequence, finalDestination, forwarded.Detail, echoRequest);
                    return null;
            }
        }

        failure = PingReply.Failure(PingReplyStatus.TtlExpired, sequence, finalDestination,
            $"The packet exceeded {MaxRouterHops} router hops without reaching {finalDestination}.", echoRequest);
        return null;
    }

    private static NetworkInterface? RouterInterfaceOwning(Router router, IPv4Address address) =>
        router.Interfaces.FirstOrDefault(i => i.IPv4Configurations.Any(c => c.Address == address));

    /// <summary>
    /// Resolves <paramref name="targetIp"/>'s MAC from <paramref name="fromInterface"/>: a cache hit
    /// returns immediately; a miss drives the full broadcast-request / unicast-reply round trip over
    /// <paramref name="topology"/> via <see cref="IEthernetTransmissionService"/> and
    /// <see cref="IArpLayer"/>, exactly as a real host would. Returns null with
    /// <paramref name="failure"/> set when resolution cannot complete.
    /// </summary>
    private MacAddress? ResolveMac(
        ITopologyView topology, NetworkInterface fromInterface, IPv4Address targetIp, int sequenceNumber, IPv4Address originalDestination,
        out PingReply? failure)
    {
        var resolution = _arp.Resolve(fromInterface, targetIp);
        if (resolution.IsResolved)
        {
            failure = null;
            return resolution.HardwareAddress;
        }

        if (resolution.HasFailed)
        {
            failure = PingReply.Failure(
                PingReplyStatus.ArpResolutionFailed, sequenceNumber, originalDestination,
                resolution.FailureReason ?? "ARP resolution failed.");
            return null;
        }

        // Broadcast the ARP request across the whole segment (through any switches). Every device
        // on the segment sees it; the one that owns the target IP answers.
        var requestSegment = _segment.TransmitAcrossSegment(topology, fromInterface, resolution.RequestFrame!);
        if (requestSegment.Deliveries.Count == 0)
        {
            failure = PingReply.Failure(
                PingReplyStatus.DestinationUnreachable, sequenceNumber, originalDestination,
                requestSegment.FirstHopDetail ?? "The ARP request could not be sent.");
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
            failure = PingReply.Failure(
                PingReplyStatus.TimedOut, sequenceNumber, originalDestination,
                $"No device on the link answered the ARP request for {targetIp}.");
            return null;
        }

        var replyTx = SendFrame(topology, responder, arpReport.ReplyFrame!, fromInterface.MacAddress);
        if (!replyTx.Success)
        {
            failure = PingReply.Failure(
                PingReplyStatus.DestinationUnreachable, sequenceNumber, originalDestination,
                replyTx.Detail ?? "The ARP reply could not be sent back.");
            return null;
        }

        _arp.HandleIncoming(replyTx.DestinationInterface!, replyTx.DeliveredFrame!);

        if (!fromInterface.ArpCache.TryGet(targetIp, out var entry))
        {
            failure = PingReply.Failure(
                PingReplyStatus.ArpResolutionFailed, sequenceNumber, originalDestination,
                $"ARP resolution for {targetIp} did not complete.");
            return null;
        }

        failure = null;
        return entry.HardwareAddress;
    }
}
