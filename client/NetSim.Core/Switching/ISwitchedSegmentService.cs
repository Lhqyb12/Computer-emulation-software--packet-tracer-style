using NetSim.Core.Ethernet;
using NetSim.Core.Networking;
using NetSim.Core.Topology;

namespace NetSim.Core.Switching;

/// <summary>
/// Delivers an <see cref="EthernetFrame"/> across a whole Layer 2 segment - the source interface,
/// through any switches in the way (learning + forwarding / flooding at each one), to every
/// non-switch interface it reaches. It is the integration seam between the pure
/// <see cref="ISwitchingEngine"/> decision and the existing single-hop
/// <see cref="IEthernetTransmissionService"/> / <see cref="Connection"/> / topology delivery
/// pipeline: it drives that pipeline once per hop and never introduces a second transport.
///
/// Upper-layer orchestrators (ARP resolution, Ping, the DHCP client's broadcast) call this instead
/// of <see cref="IEthernetTransmissionService.Transmit"/> directly, so their traffic transparently
/// crosses a switch. On a segment with no switch the result is exactly one delivery - identical to
/// a direct transmit.
///
/// A physical loop in the topology is made safe here (each switch processes a given frame at most
/// once per call, plus a hard hop cap); this is simulator stability only, <em>not</em> Spanning
/// Tree Protocol.
/// </summary>
public interface ISwitchedSegmentService
{
    /// <summary>
    /// Sends <paramref name="frame"/> out of <paramref name="sourceInterface"/> and follows it
    /// through the segment. Never throws for an expected link/forwarding condition - those are
    /// reported on the returned <see cref="SegmentDeliveryResult"/>.
    /// </summary>
    SegmentDeliveryResult TransmitAcrossSegment(
        ITopologyView topology,
        NetworkInterface sourceInterface,
        EthernetFrame frame);
}
