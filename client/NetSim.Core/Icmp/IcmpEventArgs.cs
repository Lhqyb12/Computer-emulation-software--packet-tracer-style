using NetSim.Core.IP;
using NetSim.Core.Networking;
using NetSim.Core.Packets;

namespace NetSim.Core.Icmp;

/// <summary>
/// Payload for every <see cref="IIcmpLayer"/> notification. A plain data carrier following the same
/// Core-stays-dispatch-agnostic pattern as <see cref="Arp.ArpEventArgs"/> / <see cref="IPv4PacketEventArgs"/>
/// - every field is optional, so the one type serves message-created / encapsulated / received /
/// dropped events. The real consumers - packet visualization, event timeline, monitoring, AI
/// diagnostics - arrive in later phases.
/// </summary>
public sealed class IcmpEventArgs : EventArgs
{
    public IcmpEventArgs(
        IcmpMessage? message = null,
        IPv4Packet? ipPacket = null,
        Packet? carrier = null,
        NetworkInterface? networkInterface = null,
        PacketDropReason? dropReason = null,
        string? detail = null)
    {
        Message = message;
        IPv4Packet = ipPacket;
        Carrier = carrier;
        Interface = networkInterface;
        DropReason = dropReason;
        Detail = detail;
    }

    /// <summary>The ICMP message the event is about, when one exists.</summary>
    public IcmpMessage? Message { get; }

    /// <summary>The IPv4 packet carrying the message, when the event happened in an IPv4 context.</summary>
    public IPv4Packet? IPv4Packet { get; }

    /// <summary>The generic <see cref="Packet"/> tracked by the engine, when one is involved.</summary>
    public Packet? Carrier { get; }

    /// <summary>The interface the event is scoped to (the sender for an outbound event, the receiver for an inbound one).</summary>
    public NetworkInterface? Interface { get; }

    /// <summary>The structured drop reason - set on the dropped event only.</summary>
    public PacketDropReason? DropReason { get; }

    /// <summary>Optional human-readable context, safe to show in a diagnostics panel.</summary>
    public string? Detail { get; }
}
