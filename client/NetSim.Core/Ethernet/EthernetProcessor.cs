using NetSim.Core.Packets;

namespace NetSim.Core.Ethernet;

/// <summary>
/// The first <see cref="IPacketProcessor"/> implementation: it processes a <see cref="Packet"/>
/// at the Ethernet layer <em>only</em>. It confirms the packet actually carries an
/// <see cref="EthernetFrame"/>, checks the frame is structurally valid, and classifies the
/// destination (unicast / multicast / broadcast). It performs no MAC learning, no switch
/// forwarding, no routing and no upper-layer (IPv4 / ARP / ...) handling - those are later
/// phases. Handing the decapsulated <see cref="EthernetFrame.Payload"/> to an upper-layer
/// processor, keyed by <see cref="EtherType"/>, is the Phase 18 pipeline; this class deliberately
/// stops at "the frame is a well-formed L2 unit and would be delivered up the stack".
/// </summary>
public sealed class EthernetProcessor : IPacketProcessor
{
    public PacketProcessingResult Process(Packet packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        if (packet.Payload is not EthernetFrame frame)
        {
            return PacketProcessingResult.Invalid("Packet does not carry an Ethernet frame.");
        }

        var validation = frame.Validate();
        if (!validation.IsValid)
        {
            return PacketProcessingResult.Dropped(
                EthernetDropReasons.InvalidFrame,
                string.Join("; ", validation.Errors));
        }

        return PacketProcessingResult.Delivered(Describe(frame));
    }

    private static string Describe(EthernetFrame frame) => frame.DestinationKind switch
    {
        Networking.MacAddressKind.Broadcast => $"broadcast frame, EtherType {frame.EtherType.Name}",
        Networking.MacAddressKind.Multicast => $"multicast frame to {frame.DestinationMac}, EtherType {frame.EtherType.Name}",
        _ => $"unicast frame to {frame.DestinationMac}, EtherType {frame.EtherType.Name}",
    };
}
