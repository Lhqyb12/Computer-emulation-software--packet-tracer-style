using NetSim.Core.IP;
using NetSim.Core.Networking;

namespace NetSim.Core.Transport;

/// <summary>
/// The RFC 793 / RFC 768 TCP/UDP checksum: the same one's-complement-of-one's-complement-sum
/// algorithm as <see cref="IPv4Checksum"/> / <see cref="Icmp.IcmpChecksum"/> (reused, not
/// duplicated - "create a reusable checksum implementation" per the brief), but computed over a
/// 12-byte IPv4 <em>pseudo-header</em> (source address, destination address, a zero byte, the
/// carried <see cref="ProtocolNumber"/>, and the segment/datagram length) followed by the
/// segment/datagram itself with its checksum field zeroed. The pseudo-header is never transmitted
/// on the wire in real TCP/IP either - it exists only so the checksum also protects against a
/// segment being misdelivered to the wrong address or protocol.
/// </summary>
internal static class TransportChecksum
{
    private const int PseudoHeaderLengthBytes = 12;

    /// <summary>
    /// Computes the checksum for a segment/datagram of <paramref name="protocol"/> travelling from
    /// <paramref name="source"/> to <paramref name="destination"/>. <paramref name="segment"/> must
    /// have its own checksum field already zeroed.
    /// </summary>
    public static ushort Compute(IPv4Address source, IPv4Address destination, ProtocolNumber protocol, ReadOnlySpan<byte> segment)
    {
        var buffer = new byte[PseudoHeaderLengthBytes + segment.Length];

        source.GetBytes().CopyTo(buffer, 0);
        destination.GetBytes().CopyTo(buffer, 4);
        buffer[8] = 0;
        buffer[9] = protocol.Value;
        buffer[10] = (byte)(segment.Length >> 8);
        buffer[11] = (byte)(segment.Length & 0xFF);

        segment.CopyTo(buffer.AsSpan(PseudoHeaderLengthBytes));

        return IPv4Checksum.Compute(buffer);
    }
}
