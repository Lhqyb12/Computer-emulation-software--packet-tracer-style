using NetSim.Core.Ethernet;
using NetSim.Core.Packets;

namespace NetSim.Core.IP;

/// <summary>
/// Payload for the <see cref="IIPv4Layer"/> notifications (<c>PacketCreated</c> /
/// <c>PacketEncapsulated</c> / <c>PacketProcessed</c> / <c>PacketDropped</c>). A plain data carrier,
/// following the same Core-stays-dispatch-agnostic pattern as
/// <see cref="Ethernet.EthernetFrameEventArgs"/> and <see cref="PacketEventArgs"/> - the real
/// consumers (packet visualization, event timeline, monitoring, AI diagnostics) arrive in later
/// phases.
/// </summary>
public sealed class IPv4PacketEventArgs : EventArgs
{
    public IPv4PacketEventArgs(
        IPv4Packet packet,
        EthernetFrame? frame = null,
        Packet? carrier = null,
        PacketDropReason? dropReason = null,
        string? detail = null)
    {
        Packet = packet;
        Frame = frame;
        Carrier = carrier;
        DropReason = dropReason;
        Detail = detail;
    }

    /// <summary>The IPv4 packet the event is about.</summary>
    public IPv4Packet Packet { get; }

    /// <summary>The Ethernet frame carrying the packet, when the event happened in an Ethernet context.</summary>
    public EthernetFrame? Frame { get; }

    /// <summary>The generic <see cref="Packet"/> tracked by the engine, when one is involved.</summary>
    public Packet? Carrier { get; }

    /// <summary>The structured drop reason - set on the <c>PacketDropped</c> event only.</summary>
    public PacketDropReason? DropReason { get; }

    /// <summary>Optional human-readable context, safe to show in a diagnostics panel.</summary>
    public string? Detail { get; }
}
