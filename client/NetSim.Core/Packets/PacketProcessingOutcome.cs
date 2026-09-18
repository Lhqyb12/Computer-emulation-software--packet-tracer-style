namespace NetSim.Core.Packets;

/// <summary>
/// What an <see cref="IPacketProcessor"/> decided should happen to a <see cref="Packet"/>.
/// <see cref="Transmitted"/> / <see cref="Delivered"/> are the success outcomes; the rest cause
/// the <see cref="IPacketEngine"/> to drop the packet with an appropriate
/// <see cref="PacketDropReason"/>.
/// </summary>
public enum PacketProcessingOutcome
{
    /// <summary>The packet was accepted and sent on toward its destination.</summary>
    Transmitted,

    /// <summary>The packet reached its destination endpoint.</summary>
    Delivered,

    /// <summary>The processor rejected the packet for a structured reason (see <see cref="PacketProcessingResult.DropReason"/>).</summary>
    Dropped,

    /// <summary>The processor does not implement what this packet needs yet (e.g. an unimplemented encapsulated protocol).</summary>
    Unsupported,

    /// <summary>The packet is structurally invalid and must not proceed.</summary>
    Invalid,
}
