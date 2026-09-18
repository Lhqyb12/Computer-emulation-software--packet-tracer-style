using System.Globalization;
using NetSim.Core.Common.Exceptions;
using NetSim.Core.Networking;
using NetSim.Core.Packets;

namespace NetSim.Core.IP;

/// <summary>
/// The header of an <see cref="IPv4Packet"/>. Immutable; built by <see cref="IPv4Packet"/> (its
/// constructor is <c>internal</c>) so a header's <see cref="TotalLengthBytes"/> always matches the
/// packet's real payload.
///
/// All the fields a behavioural model needs are represented: <see cref="Version"/> (fixed at 4),
/// header length (fixed at the 20-byte minimum - no options in this phase), DSCP/ECN,
/// <see cref="TotalLengthBytes"/>, <see cref="Identification"/>, <see cref="Flags"/> +
/// <see cref="FragmentOffset"/> (represented, never acted on - no fragmentation),
/// <see cref="TimeToLive"/>, <see cref="Protocol"/> and the source / destination addresses. The
/// header checksum is deliberately <em>not</em> a stored field - see <see cref="IPv4Checksum"/> for
/// why and for <see cref="ToBytes"/> / <see cref="ComputeChecksum"/>, which produce it on demand.
/// </summary>
public sealed class IPv4Header
{
    /// <summary>The IP version this header describes. Always 4 - not configurable.</summary>
    public const int Version = 4;

    /// <summary>The minimum (and, in this phase, only) IPv4 header size, in bytes.</summary>
    public const int MinimumHeaderLengthBytes = 20;

    /// <summary>The Internet Header Length in 32-bit words for a no-options header (20 bytes / 4).</summary>
    public const int InternetHeaderLengthWords = 5;

    /// <summary>The largest value the 13-bit fragment offset field can hold.</summary>
    public const int MaxFragmentOffset = 8191;

    /// <summary>The largest value the 6-bit DSCP field can hold.</summary>
    public const int MaxDscp = 63;

    /// <summary>The largest value the 2-bit ECN field can hold.</summary>
    public const int MaxEcn = 3;

    internal IPv4Header(
        IPv4Address sourceAddress,
        IPv4Address destinationAddress,
        ProtocolNumber protocol,
        byte timeToLive,
        int totalLengthBytes,
        ushort identification,
        IPv4Flags flags,
        int fragmentOffset,
        byte dscp,
        byte ecn)
    {
        if (totalLengthBytes < MinimumHeaderLengthBytes)
        {
            throw new DomainException(
                $"IPv4 total length ({totalLengthBytes}) cannot be smaller than the header ({MinimumHeaderLengthBytes} bytes).");
        }

        if (totalLengthBytes > ushort.MaxValue)
        {
            throw new DomainException($"IPv4 total length ({totalLengthBytes}) exceeds the 16-bit maximum of {ushort.MaxValue}.");
        }

        if (fragmentOffset is < 0 or > MaxFragmentOffset)
        {
            throw new DomainException($"IPv4 fragment offset must be between 0 and {MaxFragmentOffset}, but was {fragmentOffset}.");
        }

        if (dscp > MaxDscp)
        {
            throw new DomainException($"IPv4 DSCP must be between 0 and {MaxDscp}, but was {dscp}.");
        }

        if (ecn > MaxEcn)
        {
            throw new DomainException($"IPv4 ECN must be between 0 and {MaxEcn}, but was {ecn}.");
        }

        SourceAddress = sourceAddress;
        DestinationAddress = destinationAddress;
        Protocol = protocol;
        TimeToLive = timeToLive;
        TotalLengthBytes = totalLengthBytes;
        Identification = identification;
        Flags = flags;
        FragmentOffset = fragmentOffset;
        Dscp = dscp;
        Ecn = ecn;
    }

    /// <summary>The header length in bytes - always <see cref="MinimumHeaderLengthBytes"/> in this phase.</summary>
    public int HeaderLengthBytes => MinimumHeaderLengthBytes;

    /// <summary>The Internet Header Length in 32-bit words - always <see cref="InternetHeaderLengthWords"/> in this phase.</summary>
    public int InternetHeaderLength => InternetHeaderLengthWords;

    /// <summary>Differentiated Services Code Point (0-63).</summary>
    public byte Dscp { get; }

    /// <summary>Explicit Congestion Notification (0-3).</summary>
    public byte Ecn { get; }

    /// <summary>Total packet length: <see cref="HeaderLengthBytes"/> + payload length.</summary>
    public int TotalLengthBytes { get; }

    /// <summary>Length of just the payload carried after this header.</summary>
    public int PayloadLengthBytes => TotalLengthBytes - HeaderLengthBytes;

    /// <summary>The Identification field (used by fragmentation, which this phase does not implement).</summary>
    public ushort Identification { get; }

    /// <summary>The fragmentation flags - represented only.</summary>
    public IPv4Flags Flags { get; }

    /// <summary>The fragment offset (0-8191) - represented only.</summary>
    public int FragmentOffset { get; }

    /// <summary>Time To Live. Decremented by a future forwarding step; this phase only models the value.</summary>
    public byte TimeToLive { get; }

    /// <summary>The protocol carried in the payload.</summary>
    public ProtocolNumber Protocol { get; }

    /// <summary>The packet's source address.</summary>
    public IPv4Address SourceAddress { get; }

    /// <summary>The packet's destination address.</summary>
    public IPv4Address DestinationAddress { get; }

    /// <summary>True when the Don't Fragment flag is set.</summary>
    public bool DontFragment => Flags.HasFlag(IPv4Flags.DontFragment);

    /// <summary>True when the More Fragments flag is set.</summary>
    public bool MoreFragments => Flags.HasFlag(IPv4Flags.MoreFragments);

    /// <summary>
    /// The 20 header bytes in network byte order, with the checksum field zeroed. Provided for a
    /// future serialization phase; the engine does not use it. See <see cref="IPv4Checksum"/>.
    /// </summary>
    public byte[] ToBytes()
    {
        var bytes = new byte[MinimumHeaderLengthBytes];

        bytes[0] = (byte)((Version << 4) | InternetHeaderLengthWords);
        bytes[1] = (byte)((Dscp << 2) | Ecn);
        bytes[2] = (byte)(TotalLengthBytes >> 8);
        bytes[3] = (byte)(TotalLengthBytes & 0xFF);
        bytes[4] = (byte)(Identification >> 8);
        bytes[5] = (byte)(Identification & 0xFF);

        var flagsAndOffset = (ushort)(FragmentOffset & MaxFragmentOffset);
        if (DontFragment)
        {
            flagsAndOffset |= 0x4000;
        }

        if (MoreFragments)
        {
            flagsAndOffset |= 0x2000;
        }

        bytes[6] = (byte)(flagsAndOffset >> 8);
        bytes[7] = (byte)(flagsAndOffset & 0xFF);
        bytes[8] = TimeToLive;
        bytes[9] = Protocol.Value;
        // bytes[10..11] = checksum, left zero.

        var source = SourceAddress.GetBytes();
        var destination = DestinationAddress.GetBytes();
        source.CopyTo(bytes, 12);
        destination.CopyTo(bytes, 16);

        return bytes;
    }

    /// <summary>The RFC 791 header checksum for the current field values. Computed on demand, never stored.</summary>
    public ushort ComputeChecksum() => IPv4Checksum.Compute(ToBytes());

    /// <summary>
    /// Structural self-check: the total length is at least the header size and fits in 16 bits, the
    /// fragment offset / DSCP / ECN are in range, the destination address is set and the source
    /// address is a plausible host address. No protocol semantics.
    /// </summary>
    public PacketValidationResult Validate()
    {
        var errors = new List<string>();

        if (TotalLengthBytes < MinimumHeaderLengthBytes)
        {
            errors.Add($"Total length {TotalLengthBytes} is smaller than the {MinimumHeaderLengthBytes}-byte header.");
        }

        if (TotalLengthBytes > ushort.MaxValue)
        {
            errors.Add($"Total length {TotalLengthBytes} exceeds the 16-bit maximum.");
        }

        if (FragmentOffset is < 0 or > MaxFragmentOffset)
        {
            errors.Add($"Fragment offset {FragmentOffset} is outside 0-{MaxFragmentOffset}.");
        }

        if (Dscp > MaxDscp)
        {
            errors.Add($"DSCP {Dscp} is outside 0-{MaxDscp}.");
        }

        if (Ecn > MaxEcn)
        {
            errors.Add($"ECN {Ecn} is outside 0-{MaxEcn}.");
        }

        if (DestinationAddress.IsUnspecified)
        {
            errors.Add("Destination address is not set (0.0.0.0).");
        }

        if (SourceAddress.IsLimitedBroadcast || SourceAddress.IsMulticast)
        {
            errors.Add($"Source address '{SourceAddress}' is not a valid host source address.");
        }

        return errors.Count == 0 ? PacketValidationResult.Valid : new PacketValidationResult(errors);
    }

    public override string ToString() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"IPv4 {SourceAddress} -> {DestinationAddress} proto={Protocol.Name} ttl={TimeToLive} len={TotalLengthBytes}");
}
