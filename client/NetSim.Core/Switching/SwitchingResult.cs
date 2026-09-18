using NetSim.Core.Devices;
using NetSim.Core.Ethernet;
using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Vlans;

namespace NetSim.Core.Switching;

/// <summary>
/// The outcome of running one Ethernet frame through <see cref="ISwitchingEngine.ProcessFrame"/>:
/// which switch, which ingress port, the <see cref="Vlan"/> the frame was classified into, what
/// <see cref="Decision"/> was made, the resulting <see cref="Egress"/> (each port plus the exact
/// tagged/untagged frame to send out of it), a human-readable <see cref="Reason"/>, what MAC
/// learning did (<see cref="SourceLearn"/>), and - on a drop or block - the structured
/// <see cref="DropReason"/>.
///
/// It is a pure description of the decision; it does <em>not</em> move the frame. Actually
/// delivering the frame out of each egress port is <see cref="SwitchedSegmentService"/>'s job.
/// </summary>
public sealed class SwitchingResult
{
    private SwitchingResult(
        NetworkDevice switchDevice,
        NetworkInterface ingressPort,
        EthernetFrame frame,
        VlanId? vlan,
        SwitchForwardingDecision decision,
        IReadOnlyList<SwitchEgress> egress,
        string reason,
        MacLearnResult sourceLearn,
        PacketDropReason? dropReason)
    {
        Switch = switchDevice;
        IngressPort = ingressPort;
        Frame = frame;
        Vlan = vlan;
        Decision = decision;
        Egress = egress;
        Reason = reason;
        SourceLearn = sourceLearn;
        DropReason = dropReason;
    }

    public NetworkDevice Switch { get; }

    public NetworkInterface IngressPort { get; }

    public EthernetFrame Frame { get; }

    /// <summary>The VLAN the ingress port placed this frame in, or null when the frame was rejected before VLAN classification.</summary>
    public VlanId? Vlan { get; }

    public SwitchForwardingDecision Decision { get; }

    /// <summary>The port + per-port frame pairs the frame will be sent out of. Empty for a drop / block.</summary>
    public IReadOnlyList<SwitchEgress> Egress { get; }

    /// <summary>The ports the frame will be sent out of. Empty for a drop/block, exactly one for a known unicast.</summary>
    public IReadOnlyList<NetworkInterface> EgressPorts => Egress.Select(e => e.Port).ToList();

    public string Reason { get; }

    /// <summary>What learning the source MAC into the switch's table did.</summary>
    public MacLearnResult SourceLearn { get; }

    /// <summary>The structured drop / block cause - set when <see cref="Decision"/> is <see cref="SwitchForwardingDecision.Drop"/> or <see cref="SwitchForwardingDecision.Blocked"/>.</summary>
    public PacketDropReason? DropReason { get; }

    public bool IsDrop => Decision is SwitchForwardingDecision.Drop or SwitchForwardingDecision.Blocked;

    public bool IsBlocked => Decision == SwitchForwardingDecision.Blocked;

    public bool IsFlood => Decision is SwitchForwardingDecision.UnknownUnicastFlood
        or SwitchForwardingDecision.BroadcastFlood
        or SwitchForwardingDecision.MulticastFlood;

    internal static SwitchingResult Forward(
        NetworkDevice switchDevice,
        NetworkInterface ingressPort,
        EthernetFrame frame,
        VlanId vlan,
        SwitchForwardingDecision decision,
        IReadOnlyList<SwitchEgress> egress,
        string reason,
        MacLearnResult sourceLearn) =>
        new(switchDevice, ingressPort, frame, vlan, decision, egress, reason, sourceLearn, dropReason: null);

    internal static SwitchingResult Drop(
        NetworkDevice switchDevice,
        NetworkInterface ingressPort,
        EthernetFrame frame,
        PacketDropReason dropReason,
        string reason,
        MacLearnResult sourceLearn,
        VlanId? vlan = null) =>
        new(switchDevice, ingressPort, frame, vlan, SwitchForwardingDecision.Drop, [], reason, sourceLearn, dropReason);

    internal static SwitchingResult Blocked(
        NetworkDevice switchDevice,
        NetworkInterface ingressPort,
        EthernetFrame frame,
        VlanId? vlan,
        PacketDropReason dropReason,
        string reason) =>
        new(switchDevice, ingressPort, frame, vlan, SwitchForwardingDecision.Blocked, [], reason, MacLearnResult.Unchanged, dropReason);
}
