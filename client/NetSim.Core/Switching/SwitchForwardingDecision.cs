namespace NetSim.Core.Switching;

/// <summary>
/// The forwarding decision a switch makes for one received frame. Kept explicit (rather than just
/// "a list of egress ports") so a later Packet Visualization / Inspector / Diagnostics phase can
/// show <em>why</em> a frame went where it did.
/// </summary>
public enum SwitchForwardingDecision
{
    /// <summary>The frame is not forwarded (invalid ingress, destination is on the ingress port, or no eligible egress ports).</summary>
    Drop,

    /// <summary>The destination MAC is in the table; the frame is forwarded out exactly one port.</summary>
    KnownUnicast,

    /// <summary>The destination MAC is unicast but not in the table; the frame is flooded to every eligible port except ingress.</summary>
    UnknownUnicastFlood,

    /// <summary>The destination is the broadcast address; the frame is flooded to every eligible port except ingress.</summary>
    BroadcastFlood,

    /// <summary>The destination is a multicast group address; treated as flooding (no IGMP snooping in this phase).</summary>
    MulticastFlood,

    /// <summary>
    /// The frame was rejected on VLAN grounds before any forwarding was attempted - a tagged frame
    /// for a VLAN the ingress trunk does not allow, or a frame in an inactive VLAN. Distinct from
    /// <see cref="Drop"/> (a Layer 2 condition) so diagnostics can say "the VLAN boundary stopped
    /// this", and the source MAC is <em>not</em> learned.
    /// </summary>
    Blocked,
}
