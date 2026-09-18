using NetSim.Core.Packets;

namespace NetSim.Core.Switching;

/// <summary>
/// The switching layer's catalogue of structured drop reasons - plain <see cref="PacketDropReason"/>
/// values (the Phase 16 type) with an <c>SW_</c> code prefix, exactly like
/// <see cref="Ethernet.EthernetDropReasons"/>. A switch that declines to forward a frame always
/// carries an enumerable, testable cause rather than a bare string. Layer 2 causes only.
/// </summary>
public static class SwitchDropReasons
{
    /// <summary>The frame handed to the switch failed structural validation.</summary>
    public static PacketDropReason InvalidFrame { get; } = new("SW_INVALID_FRAME", "The frame handed to the switch is structurally invalid.");

    /// <summary>The device asked to switch the frame is not a switch.</summary>
    public static PacketDropReason NotASwitch { get; } = new("SW_NOT_A_SWITCH", "The device is not a switch.");

    /// <summary>The ingress interface is not part of the topology being evaluated.</summary>
    public static PacketDropReason IngressNotInTopology { get; } = new("SW_INGRESS_NOT_IN_TOPOLOGY", "The ingress port is not part of the topology.");

    /// <summary>The ingress interface does not belong to the switch.</summary>
    public static PacketDropReason IngressNotOnSwitch { get; } = new("SW_INGRESS_NOT_ON_SWITCH", "The ingress port does not belong to this switch.");

    /// <summary>The ingress interface is administratively down, link-down, or not Ethernet-capable.</summary>
    public static PacketDropReason IngressNotOperational { get; } = new("SW_INGRESS_NOT_OPERATIONAL", "The ingress port is not an operational Ethernet port.");

    /// <summary>The destination MAC resolves to the ingress port itself - never forward a frame back where it came from.</summary>
    public static PacketDropReason DestinationOnIngressPort { get; } = new("SW_DEST_ON_INGRESS", "The destination MAC is known on the ingress port.");

    /// <summary>There is no operational, connected port other than the ingress port to forward or flood out of.</summary>
    public static PacketDropReason NoEgressPorts { get; } = new("SW_NO_EGRESS_PORTS", "The switch has no eligible egress ports for this frame.");

    /// <summary>No eligible egress port carries the frame's VLAN (every other port is in a different VLAN, or not a trunk allowing it).</summary>
    public static PacketDropReason NoEgressPortsInVlan { get; } = new("SW_NO_EGRESS_PORTS_IN_VLAN", "No eligible egress port carries this frame's VLAN.");

    /// <summary>A tagged frame arrived on an ingress trunk that does not allow that VLAN - the VLAN boundary stopped it (not forwarded, not learned).</summary>
    public static PacketDropReason VlanNotAllowedOnTrunk { get; } = new("SW_VLAN_NOT_ALLOWED_ON_TRUNK", "The frame's VLAN is not allowed on the ingress trunk.");

    /// <summary>A tagged frame arrived on an access port carrying a VLAN other than that port's access VLAN.</summary>
    public static PacketDropReason TaggedFrameOnAccessPort { get; } = new("SW_TAGGED_FRAME_ON_ACCESS_PORT", "A tagged frame for another VLAN arrived on an access port.");

    /// <summary>The frame's VLAN exists but is administratively inactive, so the switch does not forward it.</summary>
    public static PacketDropReason VlanInactive { get; } = new("SW_VLAN_INACTIVE", "The frame's VLAN is administratively inactive.");

    /// <summary>
    /// The simulation's Layer 2 forwarding safety limit was reached (a switch was about to process
    /// the same flooded frame a second time, or the maximum switch-hop count was exceeded). This is
    /// a simulator-stability mechanism, not Spanning Tree Protocol - loop prevention at the network
    /// protocol level is not implemented yet.
    /// </summary>
    public static PacketDropReason ForwardingLoopPrevented { get; } = new("SW_LOOP_PREVENTED", "Layer 2 forwarding stopped by the simulation loop-safety limit.");
}
