using NetSim.Core.Icmp;
using NetSim.Core.IP;
using NetSim.Core.Networking;

namespace NetSim.Core.Routing;

/// <summary>
/// The full outcome of one <see cref="IRoutingEngine.Route"/> call - the Layer 3 forwarding
/// decision plus everything a caller needs to act on it (build the outgoing frame, resolve the
/// next hop with ARP, generate an ICMP error, raise a simulation event). Immutable.
/// </summary>
public sealed class RoutingResult
{
    private RoutingResult(
        RoutingOutcome outcome,
        IPv4Address destinationAddress,
        IPv4Packet? originalPacket,
        IPv4Packet? forwardedPacket,
        NetworkInterface? ingressInterface,
        NetworkInterface? outgoingInterface,
        IPv4Address? nextHop,
        Route? route,
        RouteLookupResult? lookup,
        IcmpType? suggestedIcmpError,
        IcmpCode? suggestedIcmpCode,
        string reason)
    {
        Outcome = outcome;
        DestinationAddress = destinationAddress;
        OriginalPacket = originalPacket;
        ForwardedPacket = forwardedPacket;
        IngressInterface = ingressInterface;
        OutgoingInterface = outgoingInterface;
        NextHop = nextHop;
        Route = route;
        Lookup = lookup;
        SuggestedIcmpError = suggestedIcmpError;
        SuggestedIcmpCode = suggestedIcmpCode;
        Reason = reason;
    }

    public RoutingOutcome Outcome { get; }

    public bool IsForward => Outcome == RoutingOutcome.Forward;

    public bool IsLocalDelivery => Outcome == RoutingOutcome.LocalDelivery;

    /// <summary>The packet's IPv4 destination.</summary>
    public IPv4Address DestinationAddress { get; }

    /// <summary>The packet as it arrived.</summary>
    public IPv4Packet? OriginalPacket { get; }

    /// <summary>The packet to transmit onward - a copy of <see cref="OriginalPacket"/> with the TTL decremented. Set only when <see cref="IsForward"/>.</summary>
    public IPv4Packet? ForwardedPacket { get; }

    /// <summary>The interface the packet arrived on.</summary>
    public NetworkInterface? IngressInterface { get; }

    /// <summary>The interface to forward out of. Set only when <see cref="IsForward"/>.</summary>
    public NetworkInterface? OutgoingInterface { get; }

    /// <summary>
    /// The address ARP must resolve for the next Layer 2 hop - the route's next-hop router, or the
    /// packet's own destination for a directly connected route. Set only when <see cref="IsForward"/>.
    /// </summary>
    public IPv4Address? NextHop { get; }

    /// <summary>The route that was selected, when one was.</summary>
    public Route? Route { get; }

    /// <summary>The full route-lookup result (present for <see cref="RoutingOutcome.Forward"/> and <see cref="RoutingOutcome.NoRoute"/>).</summary>
    public RouteLookupResult? Lookup { get; }

    /// <summary>The TTL the packet arrived with.</summary>
    public byte? OriginalTimeToLive => OriginalPacket?.TimeToLive;

    /// <summary>The TTL after the forwarding decrement. Set only when <see cref="IsForward"/>.</summary>
    public byte? NewTimeToLive => ForwardedPacket?.TimeToLive;

    /// <summary>The ICMP error type a router should generate for this outcome, or <c>null</c> when none applies.</summary>
    public IcmpType? SuggestedIcmpError { get; }

    /// <summary>The ICMP code that goes with <see cref="SuggestedIcmpError"/>.</summary>
    public IcmpCode? SuggestedIcmpCode { get; }

    /// <summary>A human-readable explanation, safe to surface in diagnostics and simulation events.</summary>
    public string Reason { get; }

    /// <summary>The identifying info an ICMP error about the offending packet needs, or <c>null</c> when no packet is available.</summary>
    public IcmpOriginalDatagramInfo? IcmpContext =>
        OriginalPacket is { } p ? IcmpOriginalDatagramInfo.FromPacket(p) : null;

    internal static RoutingResult LocalDelivery(NetworkInterface ingress, IPv4Packet packet) =>
        new(RoutingOutcome.LocalDelivery, packet.DestinationAddress, packet, forwardedPacket: null,
            ingress, outgoingInterface: null, nextHop: null, route: null, lookup: null,
            suggestedIcmpError: null, suggestedIcmpCode: null,
            $"{packet.DestinationAddress} is an address of this router - delivered locally.");

    internal static RoutingResult Forward(
        NetworkInterface ingress, IPv4Packet original, IPv4Packet forwarded, RouteLookupResult lookup) =>
        new(RoutingOutcome.Forward, original.DestinationAddress, original, forwarded,
            ingress, lookup.OutgoingInterface, lookup.NextHopAddress, lookup.Route, lookup,
            suggestedIcmpError: null, suggestedIcmpCode: null,
            $"Forwarding {original.DestinationAddress} out {lookup.OutgoingInterface?.Name} " +
            $"(TTL {original.TimeToLive} -> {forwarded.TimeToLive}); {lookup.Reason}");

    internal static RoutingResult NoRoute(NetworkInterface ingress, IPv4Packet packet, RouteLookupResult lookup) =>
        new(RoutingOutcome.NoRoute, packet.DestinationAddress, packet, forwardedPacket: null,
            ingress, outgoingInterface: null, nextHop: null, route: null, lookup,
            IcmpType.DestinationUnreachable, IcmpCode.NetworkUnreachable,
            lookup.HasRoute
                ? $"{lookup.Reason} Packet dropped."
                : $"No route to {packet.DestinationAddress} - packet dropped.");

    internal static RoutingResult TtlExpired(NetworkInterface ingress, IPv4Packet packet) =>
        new(RoutingOutcome.TtlExpired, packet.DestinationAddress, packet, forwardedPacket: null,
            ingress, outgoingInterface: null, nextHop: null, route: null, lookup: null,
            IcmpType.TimeExceeded, IcmpCode.TimeToLiveExceededInTransit,
            $"TTL {packet.TimeToLive} would expire in transit - packet dropped, ICMP Time Exceeded.");

    internal static RoutingResult InvalidPacket(NetworkInterface ingress, IPv4Packet? packet, string reason) =>
        new(RoutingOutcome.InvalidPacket, packet?.DestinationAddress ?? IPv4Address.Any, packet, forwardedPacket: null,
            ingress, outgoingInterface: null, nextHop: null, route: null, lookup: null,
            suggestedIcmpError: null, suggestedIcmpCode: null, reason);

    internal static RoutingResult InterfaceDown(NetworkInterface ingress, IPv4Packet? packet) =>
        new(RoutingOutcome.InterfaceDown, packet?.DestinationAddress ?? IPv4Address.Any, packet, forwardedPacket: null,
            ingress, outgoingInterface: null, nextHop: null, route: null, lookup: null,
            suggestedIcmpError: null, suggestedIcmpCode: null,
            $"Ingress interface '{ingress.Name}' is not operational - packet not processed.");

    internal static RoutingResult Drop(NetworkInterface ingress, IPv4Packet? packet, string reason) =>
        new(RoutingOutcome.Drop, packet?.DestinationAddress ?? IPv4Address.Any, packet, forwardedPacket: null,
            ingress, outgoingInterface: null, nextHop: null, route: null, lookup: null,
            suggestedIcmpError: null, suggestedIcmpCode: null, reason);

    public override string ToString() => $"{Outcome}: {Reason}";
}
