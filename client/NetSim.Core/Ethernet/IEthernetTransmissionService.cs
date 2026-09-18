using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Topology;

namespace NetSim.Core.Ethernet;

/// <summary>
/// Transmits an <see cref="EthernetFrame"/> out of one interface and onto the interface it is
/// physically cabled to. It is the Ethernet-layer bridge between the structure model
/// (<see cref="ITopologyView"/> / <see cref="Connection"/> / <see cref="NetworkInterface"/>) and
/// the packet model (<see cref="IPacketEngine"/>): it validates the link, then - on success -
/// creates and drives a tracked <see cref="Packet"/> through <c>Transmitted ג†’ InTransit ג†’
/// Delivered</c> so the existing engine, its events and its active-packet list all see the frame.
///
/// A single physical hop only. There is no MAC table, no flooding and no forwarding decision -
/// a frame arriving at a switch port is "delivered to that port"; what the switch then does with
/// it is Phase 25.
/// </summary>
public interface IEthernetTransmissionService
{
    /// <summary>Builds a frame (see <see cref="EthernetFrame.Create"/>) and raises <see cref="FrameCreated"/>.</summary>
    EthernetFrame CreateFrame(MacAddress source, MacAddress destination, EtherType etherType, IPacketPayload? payload = null);

    /// <summary>
    /// Attempts to send <paramref name="frame"/> from <paramref name="sourceInterface"/> across its
    /// connection. Returns a structured <see cref="EthernetTransmissionResult"/> - never throws for
    /// an expected link-level failure (interface down, no cable, non-Ethernet port, missing
    /// endpoint). <paramref name="topology"/> is the authority on physical connectivity.
    /// </summary>
    EthernetTransmissionResult Transmit(ITopologyView topology, NetworkInterface sourceInterface, EthernetFrame frame);

    /// <summary>Raised after a frame is built through <see cref="CreateFrame"/>.</summary>
    event EventHandler<EthernetFrameEventArgs>? FrameCreated;

    /// <summary>Raised after a frame has left the source interface (packet moved to <see cref="PacketState.Transmitted"/>).</summary>
    event EventHandler<EthernetFrameEventArgs>? FrameTransmitted;

    /// <summary>Raised after a frame has reached the destination interface (packet moved to <see cref="PacketState.Delivered"/>).</summary>
    event EventHandler<EthernetFrameEventArgs>? FrameDelivered;

    /// <summary>Raised after a transmission attempt was rejected, carrying the <see cref="EthernetTransmissionResult.DropReason"/>.</summary>
    event EventHandler<EthernetFrameEventArgs>? FrameDropped;
}
