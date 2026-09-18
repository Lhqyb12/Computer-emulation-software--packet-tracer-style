using NetSim.Core.Devices;
using NetSim.Core.Ethernet;
using NetSim.Core.Networking;
using NetSim.Core.Topology;

namespace NetSim.Core.Switching;

/// <summary>
/// The Layer 2 switching engine: given an <see cref="EthernetFrame"/> that has arrived on a
/// specific switch port, it learns the source MAC, consults the switch's
/// <see cref="Devices.Switch.MacAddressTable"/> and decides what to do with the frame - forward a
/// known unicast out one port, flood an unknown unicast / broadcast / multicast out every other
/// eligible port, or drop it.
///
/// It answers the question "<em>what</em> should the switch do with this frame", as a
/// <see cref="SwitchingResult"/>. It never touches cables itself - moving the frame out of the
/// chosen egress ports is <see cref="SwitchedSegmentService"/>'s job, which drives the existing
/// <see cref="IEthernetTransmissionService"/> / <see cref="Connection"/> / topology delivery
/// pipeline (no second packet transport).
///
/// Strictly Layer 2: it inspects only the Ethernet header (source / destination MAC, and - Phase
/// 26 - the 802.1Q VLAN tag). It classifies every frame into a VLAN from the ingress port's
/// configuration, learns and looks up MACs per <c>(VLAN, port)</c>, and forwards / floods only
/// within that VLAN, so two VLANs on one switch are isolated broadcast domains. It performs no IP
/// routing, no TTL handling, no ARP / DHCP protocol logic, no inter-VLAN routing, and no STP.
/// </summary>
public interface ISwitchingEngine
{
    /// <summary>
    /// Processes <paramref name="frame"/> arriving on <paramref name="ingressPort"/> of
    /// <paramref name="switchDevice"/>. Validates the ingress, ages the MAC table, learns the
    /// source MAC, classifies the destination and returns the forwarding decision. Never throws
    /// for an expected condition (invalid ingress, no egress ports, ...); those are reported on the
    /// <see cref="SwitchingResult"/>. Raises the matching events.
    /// </summary>
    SwitchingResult ProcessFrame(
        ITopologyView topology,
        NetworkDevice switchDevice,
        NetworkInterface ingressPort,
        EthernetFrame frame);

    /// <summary>
    /// Runs an explicit aging sweep over <paramref name="switchDevice"/>'s MAC table using the
    /// current simulation time, removing dynamic entries that have gone unused for longer than
    /// <see cref="IMacAddressTable.AgingTime"/> and raising <see cref="MacEntryExpired"/> for each.
    /// Returns the number of entries removed. <see cref="ProcessFrame"/> already does this on every
    /// frame; this is for a UI "age now" action or a future scheduler tick.
    /// </summary>
    int AgeMacTable(NetworkDevice switchDevice);

    /// <summary>
    /// Drops every MAC entry learned on <paramref name="port"/> - call this when a switch port goes
    /// down or is disconnected so a stale entry can no longer send traffic into a dead port.
    /// Raises <see cref="MacEntriesFlushed"/>. Returns the number of entries removed.
    /// </summary>
    int HandlePortInactive(NetworkInterface port);

    /// <summary>Raised when a frame is accepted on a switch ingress port (before the forwarding decision).</summary>
    event EventHandler<SwitchingEventArgs>? FrameReceived;

    /// <summary>Raised once the ingress port has classified the frame into a VLAN (access VLAN, 802.1Q tag, or trunk native VLAN).</summary>
    event EventHandler<SwitchingEventArgs>? FrameVlanAssigned;

    /// <summary>Raised when the VLAN boundary rejects the frame - a tagged frame for a VLAN the ingress trunk does not allow, or a frame in an inactive VLAN. The source MAC is not learned.</summary>
    event EventHandler<SwitchingEventArgs>? FrameBlocked;

    /// <summary>Raised when a source MAC is newly learned or its entry is refreshed on the same port.</summary>
    event EventHandler<SwitchingEventArgs>? MacLearned;

    /// <summary>Raised when a source MAC is seen on a different port and its entry is moved.</summary>
    event EventHandler<SwitchingEventArgs>? MacMoved;

    /// <summary>Raised for each dynamic entry removed by aging.</summary>
    event EventHandler<SwitchingEventArgs>? MacEntryExpired;

    /// <summary>Raised when entries are flushed because their port went inactive.</summary>
    event EventHandler<SwitchingEventArgs>? MacEntriesFlushed;

    /// <summary>Raised when a known unicast frame is forwarded out a single port.</summary>
    event EventHandler<SwitchingEventArgs>? FrameForwarded;

    /// <summary>Raised when an unknown unicast frame is flooded.</summary>
    event EventHandler<SwitchingEventArgs>? UnknownUnicastFlooded;

    /// <summary>Raised when a broadcast frame is flooded.</summary>
    event EventHandler<SwitchingEventArgs>? BroadcastFlooded;

    /// <summary>Raised when a multicast frame is flooded.</summary>
    event EventHandler<SwitchingEventArgs>? MulticastFlooded;

    /// <summary>Raised when the switch drops the frame, carrying the structured reason.</summary>
    event EventHandler<SwitchingEventArgs>? FrameDropped;
}
