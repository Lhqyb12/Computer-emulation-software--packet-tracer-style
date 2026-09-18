using NetSim.Core.Devices;
using NetSim.Core.Ethernet;
using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Topology;

namespace NetSim.Core.Switching;

/// <summary>
/// Default <see cref="ISwitchedSegmentService"/>. Walks the segment breadth-first (a queue, never
/// recursion, so a large or looped topology cannot overflow the stack), driving the existing
/// <see cref="IEthernetTransmissionService"/> for every physical hop and the
/// <see cref="ISwitchingEngine"/> for every switch's forwarding decision.
///
/// <para><b>Loop safety.</b> Each switch is allowed to process a given <c>TransmitAcrossSegment</c>
/// call's frame at most once (tracked by device id), and there is a hard cap on switch hops. Both
/// exist purely so a physical Layer 2 loop cannot make the simulation run forever - this is
/// <em>not</em> Spanning Tree Protocol and does not model real loop behaviour (a real looped
/// network without STP melts down; here the frame simply stops and the result says why).</para>
/// </summary>
public sealed class SwitchedSegmentService : ISwitchedSegmentService
{
    /// <summary>Absolute ceiling on switch traversals for one delivery - a backstop behind the per-switch visited set.</summary>
    public const int MaxSwitchHops = 64;

    private readonly IEthernetTransmissionService _ethernet;
    private readonly ISwitchingEngine _switchingEngine;

    public SwitchedSegmentService(IEthernetTransmissionService ethernet, ISwitchingEngine switchingEngine)
    {
        ArgumentNullException.ThrowIfNull(ethernet);
        ArgumentNullException.ThrowIfNull(switchingEngine);
        _ethernet = ethernet;
        _switchingEngine = switchingEngine;
    }

    public SegmentDeliveryResult TransmitAcrossSegment(
        ITopologyView topology,
        NetworkInterface sourceInterface,
        EthernetFrame frame)
    {
        ArgumentNullException.ThrowIfNull(topology);
        ArgumentNullException.ThrowIfNull(sourceInterface);
        ArgumentNullException.ThrowIfNull(frame);

        var deliveries = new List<EndpointDelivery>();
        var switchingResults = new List<SwitchingResult>();
        var visitedSwitches = new HashSet<Common.EntityId>();
        var queue = new Queue<Hop>();
        var loopTriggered = false;
        string? loopReason = null;

        var firstHop = _ethernet.Transmit(topology, sourceInterface, frame);
        if (!firstHop.IsSuccess)
        {
            return SegmentDeliveryResult.FirstHopFailed(
                firstHop.DropReason ?? Ethernet.EthernetDropReasons.InvalidFrame, firstHop.Detail);
        }

        Route(firstHop, hop: 1, queue, deliveries);

        while (queue.Count > 0)
        {
            var (switchDevice, ingress, hopFrame, hopCount) = queue.Dequeue();

            if (hopCount > MaxSwitchHops)
            {
                loopTriggered = true;
                loopReason = $"Switch-hop cap ({MaxSwitchHops}) exceeded at '{switchDevice.Name}'.";
                continue;
            }

            if (!visitedSwitches.Add(switchDevice.Id))
            {
                loopTriggered = true;
                loopReason = $"'{switchDevice.Name}' would process the same frame again - physical Layer 2 loop; forwarding stopped.";
                continue;
            }

            var switchResult = _switchingEngine.ProcessFrame(topology, switchDevice, ingress, hopFrame);
            switchingResults.Add(switchResult);
            if (switchResult.IsDrop)
            {
                continue;
            }

            // Each egress carries its own per-port wire frame (802.1Q-tagged out a trunk for a
            // non-native VLAN, untagged otherwise) computed by the switching engine - so the VLAN
            // survives a trunk hop and a host behind an access port always gets an untagged frame.
            foreach (var egress in switchResult.Egress)
            {
                var tx = _ethernet.Transmit(topology, egress.Port, egress.Frame);
                if (tx.IsSuccess)
                {
                    Route(tx, hopCount + 1, queue, deliveries);
                }
            }
        }

        return new SegmentDeliveryResult(
            deliveries, switchingResults, firstHopDropReason: null, firstHopDetail: null, loopTriggered, loopReason);
    }

    // Classifies where a just-delivered hop landed: another switch (enqueue) or an endpoint (record).
    private static void Route(EthernetTransmissionResult tx, int hop, Queue<Hop> queue, List<EndpointDelivery> deliveries)
    {
        var far = tx.DestinationInterface!;
        var deliveredFrame = tx.Packet!.Payload as EthernetFrame ?? throw new InvalidOperationException(
            "The Ethernet layer delivered a packet that does not carry a frame.");

        if (far.Device is Switch farSwitch)
        {
            queue.Enqueue(new Hop(farSwitch, far, deliveredFrame, hop));
        }
        else
        {
            deliveries.Add(new EndpointDelivery(far, deliveredFrame, tx.Packet!));
        }
    }

    private readonly record struct Hop(Switch SwitchDevice, NetworkInterface Ingress, EthernetFrame Frame, int Count);
}
