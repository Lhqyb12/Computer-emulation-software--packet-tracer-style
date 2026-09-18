using NetSim.Core.IP;

namespace NetSim.Core.Icmp;

/// <summary>
/// The RFC 792 ICMP checksum - the same 16-bit one's-complement-of-one's-complement-sum algorithm
/// as the IPv4 header checksum (RFC 791), just run over the whole ICMP message instead of a fixed
/// 20-byte header. Reuses <see cref="IPv4Checksum"/> (internal to this assembly, so visible here)
/// rather than duplicating the summation logic - the brief's "create a reusable checksum
/// implementation ... do not duplicate checksum logic" instruction.
/// </summary>
internal static class IcmpChecksum
{
    /// <summary>Computes the checksum over <paramref name="message"/>, which must have its checksum field zeroed.</summary>
    public static ushort Compute(ReadOnlySpan<byte> message) => IPv4Checksum.Compute(message);
}
