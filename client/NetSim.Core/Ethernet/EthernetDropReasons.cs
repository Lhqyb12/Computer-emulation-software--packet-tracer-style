using NetSim.Core.Packets;

namespace NetSim.Core.Ethernet;

/// <summary>
/// The Ethernet layer's catalogue of structured drop reasons. These are plain
/// <see cref="PacketDropReason"/> values (the Phase 16 type) with an <c>ETH_</c> code prefix -
/// exactly the "each protocol layer contributes its own catalogue" extension point the packet
/// engine anticipated, so a dropped frame always carries an enumerable, testable cause rather
/// than a bare string. Link-layer causes only: no IP / routing / ARP reasons belong here.
/// </summary>
public static class EthernetDropReasons
{
    /// <summary>The frame failed structural validation (bad source MAC, unset EtherType, broken payload chain).</summary>
    public static PacketDropReason InvalidFrame { get; } = new("ETH_INVALID_FRAME", "The Ethernet frame failed structural validation.");

    /// <summary>The transmitting interface is not administratively enabled or its link is down.</summary>
    public static PacketDropReason SourceInterfaceDown { get; } = new("ETH_SRC_IF_DOWN", "The source interface is not operational.");

    /// <summary>The receiving interface on the far side of the cable is not operational.</summary>
    public static PacketDropReason DestinationInterfaceDown { get; } = new("ETH_DST_IF_DOWN", "The destination interface is not operational.");

    /// <summary>The source interface has no cable attached.</summary>
    public static PacketDropReason NoPhysicalConnection { get; } = new("ETH_NO_LINK", "The source interface is not connected to anything.");

    /// <summary>The interface does not participate in the Ethernet layer (e.g. a Serial or Console port).</summary>
    public static PacketDropReason NotEthernetCapable { get; } = new("ETH_IF_NOT_ETHERNET", "The interface is not Ethernet-capable.");

    /// <summary>The source interface has no MAC address to transmit from.</summary>
    public static PacketDropReason SourceMacMissing { get; } = new("ETH_NO_SRC_MAC", "The source interface has no MAC address.");

    /// <summary>The source interface is not part of the topology the transmission was evaluated against.</summary>
    public static PacketDropReason SourceInterfaceNotInTopology { get; } = new("ETH_SRC_IF_UNKNOWN", "The source interface is not part of the topology.");

    /// <summary>The connection's far endpoint could not be resolved to a live interface in the topology.</summary>
    public static PacketDropReason InvalidEndpoint { get; } = new("ETH_INVALID_ENDPOINT", "The connection's destination endpoint could not be resolved.");
}
