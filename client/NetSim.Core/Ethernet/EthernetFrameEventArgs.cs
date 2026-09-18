using NetSim.Core.Networking;
using NetSim.Core.Packets;

namespace NetSim.Core.Ethernet;

/// <summary>
/// Payload for the <see cref="IEthernetTransmissionService"/> notifications
/// (<c>FrameCreated</c> / <c>FrameTransmitted</c> / <c>FrameDelivered</c> / <c>FrameDropped</c>).
/// A plain data carrier, following the same Core-stays-dispatch-agnostic pattern as
/// <see cref="PacketEventArgs"/> and <c>Network.TopologyChanged</c> - the real consumers (packet
/// visualization, event timeline, monitoring, AI diagnostics) arrive in later phases.
/// </summary>
public sealed class EthernetFrameEventArgs : EventArgs
{
    public EthernetFrameEventArgs(
        EthernetFrame frame,
        NetworkInterface? sourceInterface = null,
        NetworkInterface? destinationInterface = null,
        Packet? packet = null,
        PacketDropReason? dropReason = null,
        string? detail = null)
    {
        Frame = frame;
        SourceInterface = sourceInterface;
        DestinationInterface = destinationInterface;
        Packet = packet;
        DropReason = dropReason;
        Detail = detail;
    }

    public EthernetFrame Frame { get; }

    public NetworkInterface? SourceInterface { get; }

    public NetworkInterface? DestinationInterface { get; }

    /// <summary>The tracked packet carrying the frame, when one exists (transmit/deliver events).</summary>
    public Packet? Packet { get; }

    /// <summary>The structured drop reason - set on the <c>FrameDropped</c> event only.</summary>
    public PacketDropReason? DropReason { get; }

    public string? Detail { get; }
}
