using NetSim.Core.Networking;
using NetSim.Core.Packets;

namespace NetSim.Core.Switching;

/// <summary>
/// The outcome of <see cref="ISwitchedSegmentService.TransmitAcrossSegment"/>: every non-switch
/// interface the frame reached (<see cref="Deliveries"/>), the per-switch
/// <see cref="SwitchingResults"/> collected on the way, and - if it fired - the loop-safety
/// signal. When there is no switch on the segment this collapses to exactly one delivery, i.e.
/// the same result a bare <see cref="Ethernet.IEthernetTransmissionService.Transmit"/> call would
/// have produced.
/// </summary>
public sealed class SegmentDeliveryResult
{
    public SegmentDeliveryResult(
        IReadOnlyList<EndpointDelivery> deliveries,
        IReadOnlyList<SwitchingResult> switchingResults,
        PacketDropReason? firstHopDropReason,
        string? firstHopDetail,
        bool loopSafetyTriggered,
        string? loopSafetyReason)
    {
        Deliveries = deliveries;
        SwitchingResults = switchingResults;
        FirstHopDropReason = firstHopDropReason;
        FirstHopDetail = firstHopDetail;
        LoopSafetyTriggered = loopSafetyTriggered;
        LoopSafetyReason = loopSafetyReason;
    }

    /// <summary>Every non-switch interface the frame was delivered to (in delivery order).</summary>
    public IReadOnlyList<EndpointDelivery> Deliveries { get; }

    /// <summary>The forwarding decision each switch on the segment made, in the order they processed the frame.</summary>
    public IReadOnlyList<SwitchingResult> SwitchingResults { get; }

    /// <summary>Set when the very first hop out of the source interface failed - nothing entered the segment.</summary>
    public PacketDropReason? FirstHopDropReason { get; }

    public string? FirstHopDetail { get; }

    /// <summary>
    /// True when Layer 2 forwarding was stopped by the simulation loop-safety limit (a physical
    /// loop in the topology). This is simulator protection, not STP.
    /// </summary>
    public bool LoopSafetyTriggered { get; }

    public string? LoopSafetyReason { get; }

    public bool ReachedAnyEndpoint => Deliveries.Count > 0;

    /// <summary>The delivery whose receiving interface is <paramref name="target"/>, or null.</summary>
    public EndpointDelivery? DeliveryTo(NetworkInterface target) =>
        Deliveries.FirstOrDefault(d => d.DestinationInterface.Id == target.Id);

    /// <summary>The delivery whose receiving interface owns <paramref name="mac"/>, or null.</summary>
    public EndpointDelivery? DeliveryToMac(MacAddress mac) =>
        Deliveries.FirstOrDefault(d => d.DestinationInterface.MacAddress == mac);

    internal static SegmentDeliveryResult FirstHopFailed(PacketDropReason reason, string? detail) =>
        new([], [], reason, detail, loopSafetyTriggered: false, loopSafetyReason: null);
}
