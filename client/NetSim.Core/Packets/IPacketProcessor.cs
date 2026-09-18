namespace NetSim.Core.Packets;

/// <summary>
/// Processes a <see cref="Packet"/> at one protocol layer and reports what should happen to it,
/// as a <see cref="PacketProcessingResult"/>. Phase 16 defines only this contract - the first
/// implementation is the Ethernet processor in Phase 17, with IPv4 / ARP / ... processors
/// following the same shape.
///
/// A processor <em>decides</em> the outcome; it does not mutate the packet's
/// <see cref="Packet.State"/> or raise events. The <see cref="IPacketEngine"/> is what applies
/// the resulting state change and notifies observers, so every processor gets that behaviour
/// for free and consistently.
/// </summary>
public interface IPacketProcessor
{
    PacketProcessingResult Process(Packet packet);
}
