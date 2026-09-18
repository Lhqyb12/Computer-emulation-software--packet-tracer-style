using NetSim.Core.Networking;
using NetSim.Core.Packets;

namespace NetSim.Core.Udp;

/// <summary>
/// The rich, human-readable outcome of <see cref="IUdpLayer.HandleIncoming"/> - mirrors
/// <see cref="Icmp.IcmpProcessingReport"/>: what was received, whether the receiving interface owned
/// the destination, and whether it was actually delivered to a bound endpoint.
/// </summary>
public sealed class UdpProcessingReport
{
    private UdpProcessingReport(
        bool isSuccess, PacketDropReason? dropReason, UdpDatagram? datagram, bool localInterfaceOwnsDestination,
        UdpBinding? deliveredTo, string description)
    {
        IsSuccess = isSuccess;
        DropReason = dropReason;
        Datagram = datagram;
        LocalInterfaceOwnsDestination = localInterfaceOwnsDestination;
        DeliveredTo = deliveredTo;
        Description = description;
    }

    /// <summary>True when the datagram passed structural/checksum validation - independent of whether it was actually delivered.</summary>
    public bool IsSuccess { get; }

    /// <summary>Why the datagram was dropped - null when <see cref="IsSuccess"/>.</summary>
    public PacketDropReason? DropReason { get; }

    /// <summary>The received datagram - null only for a dropped datagram.</summary>
    public UdpDatagram? Datagram { get; }

    /// <summary>True when the receiving interface owns the IPv4 packet's destination address.</summary>
    public bool LocalInterfaceOwnsDestination { get; }

    /// <summary>True when a bound endpoint received the datagram.</summary>
    public bool IsDelivered => DeliveredTo is not null;

    /// <summary>The endpoint the datagram was delivered to - set only when <see cref="IsDelivered"/>.</summary>
    public UdpBinding? DeliveredTo { get; }

    public string Description { get; }

    internal static UdpProcessingReport Dropped(PacketDropReason reason, string detail) =>
        new(false, reason, null, false, null, $"UDP datagram dropped ({reason.Code}): {detail}");

    internal static UdpProcessingReport NotOwned(UdpDatagram datagram, IPv4Address destination) =>
        new(true, null, datagram, false, null,
            $"UDP datagram for {destination}:{datagram.DestinationPort} received on an interface that does not own that address.");

    internal static UdpProcessingReport PortUnavailable(UdpDatagram datagram) =>
        new(true, null, datagram, true, null,
            $"No endpoint is bound to UDP port {datagram.DestinationPort}. Datagram discarded.");

    internal static UdpProcessingReport Delivered(UdpDatagram datagram, UdpBinding binding) =>
        new(true, null, datagram, true, binding, $"UDP datagram delivered to {binding}.");
}
