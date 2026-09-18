using NetSim.Core.Devices;
using NetSim.Core.IP;
using NetSim.Core.Networking;

namespace NetSim.Core.Routing;

/// <summary>
/// Carries a routing decision to a subscriber of the <see cref="IRoutingEngine"/>'s events - the
/// structured record a future timeline / monitoring / diagnostics phase needs (brief sections 40,
/// 72). It reuses the existing event infrastructure pattern (mirrors
/// <see cref="Arp.ArpEventArgs"/> / <see cref="Icmp.IcmpEventArgs"/>); the routing engine does not
/// introduce a logging system of its own.
/// </summary>
public sealed class RoutingEventArgs : EventArgs
{
    public RoutingEventArgs(
        Router router,
        NetworkInterface? ingressInterface,
        IPv4Packet? packet,
        RoutingResult? result = null,
        string? detail = null)
    {
        Router = router;
        IngressInterface = ingressInterface;
        Packet = packet;
        Result = result;
        Detail = detail ?? result?.Reason ?? string.Empty;
    }

    /// <summary>The router that made the decision.</summary>
    public Router Router { get; }

    /// <summary>The interface the packet arrived on, when known.</summary>
    public NetworkInterface? IngressInterface { get; }

    /// <summary>The IPv4 packet the event is about, when known.</summary>
    public IPv4Packet? Packet { get; }

    /// <summary>The full routing result, for the events raised once a decision has been reached.</summary>
    public RoutingResult? Result { get; }

    /// <summary>A human-readable description of the event.</summary>
    public string Detail { get; }
}
