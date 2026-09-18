using System.Linq;
using NetSim.Core.Devices;
using NetSim.Core.IP;
using NetSim.Core.Networking;

namespace NetSim.Core.Routing;

/// <summary>
/// Default <see cref="IRoutingEngine"/>. Stateless apart from its event subscribers - the routing
/// state lives in each <see cref="Router.RoutingTable"/>. Mirrors <see cref="Arp.ArpLayer"/> /
/// <see cref="Icmp.IcmpLayer"/>: it <em>decides</em> and raises events; something else applies the
/// decision on the wire.
/// </summary>
public sealed class RoutingEngine : IRoutingEngine
{
    public event EventHandler<RoutingEventArgs>? PacketReceived;

    public event EventHandler<RoutingEventArgs>? LocalDelivery;

    public event EventHandler<RoutingEventArgs>? RouteSelected;

    public event EventHandler<RoutingEventArgs>? PacketForwarded;

    public event EventHandler<RoutingEventArgs>? NoRouteToDestination;

    public event EventHandler<RoutingEventArgs>? TimeToLiveExpired;

    public event EventHandler<RoutingEventArgs>? PacketDropped;

    public RoutingResult Route(Router router, NetworkInterface ingressInterface, IPv4Packet packet)
    {
        ArgumentNullException.ThrowIfNull(router);
        ArgumentNullException.ThrowIfNull(ingressInterface);
        ArgumentNullException.ThrowIfNull(packet);

        PacketReceived?.Invoke(this, new RoutingEventArgs(
            router, ingressInterface, packet,
            detail: $"{router.Name} received IPv4 packet {packet} on {ingressInterface.Name}"));

        // 1. The interface the packet came in on must be usable.
        if (!ingressInterface.IsOperational)
        {
            return Dropped(RoutingResult.InterfaceDown(ingressInterface, packet), PacketDropped, router);
        }

        // 2. The packet must be structurally valid (this also catches a TTL that is already 0).
        var validation = packet.Validate();
        if (!validation.IsValid)
        {
            return Dropped(
                RoutingResult.InvalidPacket(ingressInterface, packet, string.Join("; ", validation.Errors)),
                PacketDropped, router);
        }

        // 3. Connected routes always reflect the router's current interface configuration.
        router.SyncConnectedRoutes();

        var destination = packet.DestinationAddress;

        // 4. Addressed to the router itself? Deliver locally - never route a packet to one of our
        //    own addresses (brief section 17).
        if (RouterOwns(router, destination))
        {
            var local = RoutingResult.LocalDelivery(ingressInterface, packet);
            LocalDelivery?.Invoke(this, new RoutingEventArgs(router, ingressInterface, packet, local));
            return local;
        }

        // 5. The router is not a Layer 2 bridge: a broadcast/multicast destination stays in its
        //    Layer 2 domain and is never forwarded (brief section 35).
        if (destination.IsLimitedBroadcast || destination.IsMulticast || IsDirectedBroadcast(router, destination))
        {
            return Dropped(
                RoutingResult.Drop(ingressInterface, packet,
                    $"{destination} is a broadcast/multicast address - a router does not bridge it."),
                PacketDropped, router);
        }

        // 6. Routing table lookup - longest prefix match, default route as the fallback. A route
        //    that matches but cannot be resolved right now (transit interface down, next hop
        //    unreachable) is treated as "no usable route" here, but the reason still says a route
        //    was configured (brief section 30).
        var lookup = router.RoutingTable.FindBestRoute(destination);
        if (!lookup.HasRoute || !lookup.IsResolvable)
        {
            var noRoute = RoutingResult.NoRoute(ingressInterface, packet, lookup);
            NoRouteToDestination?.Invoke(this, new RoutingEventArgs(router, ingressInterface, packet, noRoute));
            return noRoute;
        }

        RouteSelected?.Invoke(this, new RoutingEventArgs(
            router, ingressInterface, packet,
            detail: $"{router.Name}: {lookup.Reason}"));

        // 7. TTL handling: a packet arriving with TTL 1 (or 0) cannot survive the decrement.
        if (packet.TimeToLive <= 1)
        {
            var expired = RoutingResult.TtlExpired(ingressInterface, packet);
            TimeToLiveExpired?.Invoke(this, new RoutingEventArgs(router, ingressInterface, packet, expired));
            return expired;
        }

        // 8. Decrement the TTL (the IPv4 header checksum is recomputed on demand by IPv4Header -
        //    there is no stored checksum to fix up) and forward.
        var forwarded = packet.WithDecrementedTimeToLive();
        var result = RoutingResult.Forward(ingressInterface, packet, forwarded, lookup);
        PacketForwarded?.Invoke(this, new RoutingEventArgs(router, ingressInterface, packet, result));
        return result;
    }

    private RoutingResult Dropped(RoutingResult result, EventHandler<RoutingEventArgs>? handler, Router router)
    {
        handler?.Invoke(this, new RoutingEventArgs(router, result.IngressInterface, result.OriginalPacket, result));
        return result;
    }

    private static bool RouterOwns(Router router, IPv4Address address) =>
        router.Interfaces
            .SelectMany(i => i.IPv4Configurations)
            .Any(c => c.Address == address);

    private static bool IsDirectedBroadcast(Router router, IPv4Address address) =>
        router.Interfaces
            .SelectMany(i => i.IPv4Configurations)
            .Any(c => c.Network.BroadcastAddress == address);
}
