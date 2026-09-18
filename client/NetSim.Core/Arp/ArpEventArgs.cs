using NetSim.Core.Ethernet;
using NetSim.Core.Networking;
using NetSim.Core.Packets;

namespace NetSim.Core.Arp;

/// <summary>
/// Payload for every <see cref="IArpLayer"/> notification. A plain data carrier following the same
/// Core-stays-dispatch-agnostic pattern as <see cref="IP.IPv4PacketEventArgs"/> - every field is
/// optional, so the one type serves the packet events (request/reply created, encapsulated,
/// received), the resolution events (started / succeeded / failed) and the cache events (entry
/// learned / updated). The real consumers - packet visualization, event timeline, monitoring, AI
/// diagnostics - arrive in later phases.
/// </summary>
public sealed class ArpEventArgs : EventArgs
{
    public ArpEventArgs(
        ArpPacket? packet = null,
        EthernetFrame? frame = null,
        Packet? carrier = null,
        NetworkInterface? networkInterface = null,
        ArpCacheEntry? entry = null,
        IPv4Address? protocolAddress = null,
        MacAddress? hardwareAddress = null,
        PacketDropReason? dropReason = null,
        string? detail = null)
    {
        Packet = packet;
        Frame = frame;
        Carrier = carrier;
        Interface = networkInterface;
        Entry = entry;
        ProtocolAddress = protocolAddress;
        HardwareAddress = hardwareAddress;
        DropReason = dropReason;
        Detail = detail;
    }

    /// <summary>The ARP message the event is about, when one exists (null for a cache-hit resolution / expiry event).</summary>
    public ArpPacket? Packet { get; }

    /// <summary>The Ethernet frame carrying the message, when the event happened in an Ethernet context.</summary>
    public EthernetFrame? Frame { get; }

    /// <summary>The generic <see cref="Packet"/> tracked by the engine, when one is involved.</summary>
    public Packet? Carrier { get; }

    /// <summary>The interface the event is scoped to (the sender for an outbound event, the receiver for an inbound one).</summary>
    public NetworkInterface? Interface { get; }

    /// <summary>The cache entry involved - set on the learned / updated / succeeded events.</summary>
    public ArpCacheEntry? Entry { get; }

    /// <summary>The IPv4 address being resolved / learned, when relevant.</summary>
    public IPv4Address? ProtocolAddress { get; }

    /// <summary>The resolved MAC address, when relevant.</summary>
    public MacAddress? HardwareAddress { get; }

    /// <summary>The structured drop reason - set on the dropped / resolution-failed events only.</summary>
    public PacketDropReason? DropReason { get; }

    /// <summary>Optional human-readable context, safe to show in a diagnostics panel.</summary>
    public string? Detail { get; }
}
