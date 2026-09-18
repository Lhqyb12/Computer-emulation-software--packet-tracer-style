namespace NetSim.Core.Arp;

/// <summary>
/// The ARP <c>oper</c> field - what an <see cref="ArpPacket"/> is asking for or answering. Only
/// the two operations this phase models; the numeric values are the on-the-wire assignments
/// (RFC 826), so a packet round-trips through them unchanged. Represented as a strong type rather
/// than a raw number / string anywhere in the system.
/// </summary>
public enum ArpOperation
{
    /// <summary>"Who has this IPv4 address?" - broadcast.</summary>
    Request = 1,

    /// <summary>"That IPv4 address is at this MAC." - normally unicast to the requester.</summary>
    Reply = 2,
}
