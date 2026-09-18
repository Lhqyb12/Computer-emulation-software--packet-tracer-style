using NetSim.Core.Ethernet;
using NetSim.Core.Networking;

namespace NetSim.Core.Switching;

/// <summary>
/// One egress instruction from the switching engine: the <see cref="Port"/> the frame leaves by,
/// and the exact <see cref="Frame"/> to put on that port. The frame is the per-port wire form -
/// 802.1Q-tagged when the port is a trunk carrying the frame's VLAN as a non-native VLAN,
/// untagged when the port is an access port or the trunk's native VLAN. The engine computes this
/// so <see cref="SwitchedSegmentService"/> only has to transmit each pair, and every egress path
/// stays independently traceable.
/// </summary>
public sealed record SwitchEgress(NetworkInterface Port, EthernetFrame Frame);
