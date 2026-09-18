namespace NetSim.Core.IP;

/// <summary>
/// The standard RFC 791 IPv4 header checksum: the 16-bit one's-complement of the one's-complement
/// sum of the header taken as a sequence of 16-bit words.
///
/// This simulation is <em>behavioural</em>, not a wire-format codec (the same stance as
/// <see cref="Ethernet.EthernetFrame"/> carrying no FCS): a real checksum is therefore never a
/// stored field on <see cref="IPv4Header"/> and never gates validity. This helper - together with
/// <see cref="IPv4Header.ToBytes"/> and <see cref="IPv4Header.ComputeChecksum"/> - provides the
/// correct algorithm so a future serialization / packet-inspector phase has it ready, without the
/// rest of the engine having to model framing.
/// </summary>
internal static class IPv4Checksum
{
    public static ushort Compute(ReadOnlySpan<byte> header)
    {
        uint sum = 0;

        for (var i = 0; i + 1 < header.Length; i += 2)
        {
            sum += (uint)((header[i] << 8) | header[i + 1]);
        }

        if ((header.Length & 1) == 1)
        {
            sum += (uint)(header[^1] << 8);
        }

        while ((sum >> 16) != 0)
        {
            sum = (sum & 0xFFFF) + (sum >> 16);
        }

        return (ushort)~sum;
    }
}
