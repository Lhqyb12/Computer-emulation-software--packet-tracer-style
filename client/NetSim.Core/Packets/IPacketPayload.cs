namespace NetSim.Core.Packets;

/// <summary>
/// One layer of data carried by a <see cref="Packet"/>. Payloads nest: a future Ethernet frame
/// (Phase 17) is an <see cref="IPacketPayload"/> whose <see cref="EncapsulatedPayload"/> is the
/// IPv4 packet it carries, which in turn encapsulates a segment, and so on down to a leaf
/// <see cref="RawPayload"/>. The packet engine only ever sees this protocol-agnostic shape - no
/// header field (MAC, EtherType, addresses, ports) is modelled here; those belong to the
/// concrete payload types later phases add.
/// </summary>
public interface IPacketPayload
{
    /// <summary>Short protocol label for diagnostics and a future inspector UI, e.g. "Raw", "Ethernet", "IPv4".</summary>
    string PayloadType { get; }

    /// <summary>
    /// Total simulated size in bytes: this layer plus everything it encapsulates. Lets a later
    /// phase enforce MTU / oversized-frame rules without this phase modelling the wire format.
    /// </summary>
    int Length { get; }

    /// <summary>The next protocol layer down, or null at a leaf payload.</summary>
    IPacketPayload? EncapsulatedPayload { get; }

    /// <summary>
    /// Structural self-check only (is this layer internally consistent, is its encapsulation
    /// chain intact). Never protocol semantics. Returns a report rather than throwing.
    /// </summary>
    PacketValidationResult Validate();
}
