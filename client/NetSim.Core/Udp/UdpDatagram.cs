using NetSim.Core.Icmp;
using NetSim.Core.IP;
using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Transport;

namespace NetSim.Core.Udp;

/// <summary>
/// A simulated UDP datagram (RFC 768): Source Port, Destination Port, Length, Checksum and an
/// opaque application payload. Like <see cref="Icmp.IcmpMessage"/> it is a leaf
/// <see cref="IPacketPayload"/> - <see cref="EncapsulatedPayload"/> is the carried application data,
/// never another protocol layer - and it rides as the <see cref="IPv4Packet.Payload"/> of an IPv4
/// packet whose <see cref="IPv4Header.Protocol"/> is <see cref="ProtocolNumber.Udp"/> (17).
///
/// UDP is connectionless: this type carries no state across datagrams (no sequence numbers, no
/// handshake, no connection) - each datagram is entirely self-contained.
///
/// Unlike <see cref="IcmpMessage"/>'s self-contained checksum, a UDP checksum is computed over a
/// pseudo-header that includes the source/destination IPv4 addresses (see
/// <see cref="TransportChecksum"/>), so those addresses must be known at creation time even though
/// neither address is itself a field of the UDP header - exactly like real UDP, where the sending
/// host's IP stack knows both addresses before it ever builds the header.
/// </summary>
public sealed class UdpDatagram : IPacketPayload
{
    /// <summary>Simulated header size: source port(2) + destination port(2) + length(2) + checksum(2).</summary>
    public const int HeaderSizeBytes = 8;

    private UdpDatagram(Port sourcePort, Port destinationPort, IPacketPayload payload, ushort checksumOverride)
    {
        SourcePort = sourcePort;
        DestinationPort = destinationPort;
        Payload = payload;
        Checksum = checksumOverride;
    }

    /// <summary>
    /// Builds a datagram with a correctly-computed checksum for the given source/destination
    /// addresses. An invalid datagram never exists - there is no structural rule to violate beyond
    /// what <see cref="Port"/> and <paramref name="payload"/> already enforce.
    /// </summary>
    public static UdpDatagram Create(
        IPv4Address source, IPv4Address destination, Port sourcePort, Port destinationPort, IPacketPayload? payload = null)
    {
        var body = payload ?? RawPayload.Empty;
        var checksum = ComputeChecksumInternal(source, destination, sourcePort, destinationPort, body, checksumOverride: 0);
        return new UdpDatagram(sourcePort, destinationPort, body, checksum);
    }

    public Port SourcePort { get; }

    public Port DestinationPort { get; }

    /// <summary>The carried application data - never null (<see cref="RawPayload.Empty"/> for an empty datagram).</summary>
    public IPacketPayload Payload { get; }

    /// <summary>The RFC 768 checksum, correct as of the addresses given to <see cref="Create"/>. See <see cref="HasValidChecksum"/>.</summary>
    public ushort Checksum { get; }

    /// <summary>Recomputes the checksum for the given addresses from the current field values.</summary>
    public ushort ComputeChecksum(IPv4Address source, IPv4Address destination) =>
        ComputeChecksumInternal(source, destination, SourcePort, DestinationPort, Payload, checksumOverride: 0);

    /// <summary>True when <see cref="Checksum"/> matches what the current fields compute to for the given addresses.</summary>
    public bool HasValidChecksum(IPv4Address source, IPv4Address destination) =>
        Checksum == ComputeChecksum(source, destination);

    /// <summary>
    /// Returns a copy carrying <paramref name="checksum"/> instead of the correct one - simulates a
    /// datagram corrupted in transit, mirroring <see cref="IcmpMessage.WithChecksum"/>.
    /// </summary>
    public UdpDatagram WithChecksum(ushort checksum) => new(SourcePort, DestinationPort, Payload, checksum);

    private static ushort ComputeChecksumInternal(
        IPv4Address source, IPv4Address destination, Port sourcePort, Port destinationPort, IPacketPayload payload, ushort checksumOverride)
    {
        var bytes = ToBytesCore(sourcePort, destinationPort, payload, checksumOverride);
        return TransportChecksum.Compute(source, destination, ProtocolNumber.Udp, bytes);
    }

    private static byte[] ToBytesCore(Port sourcePort, Port destinationPort, IPacketPayload payload, ushort checksum)
    {
        var length = HeaderSizeBytes + payload.Length;
        var bytes = new byte[length];

        bytes[0] = (byte)(sourcePort.Value >> 8);
        bytes[1] = (byte)(sourcePort.Value & 0xFF);
        bytes[2] = (byte)(destinationPort.Value >> 8);
        bytes[3] = (byte)(destinationPort.Value & 0xFF);
        bytes[4] = (byte)(length >> 8);
        bytes[5] = (byte)(length & 0xFF);
        bytes[6] = (byte)(checksum >> 8);
        bytes[7] = (byte)(checksum & 0xFF);

        if (payload is RawPayload raw)
        {
            raw.Data.Span.CopyTo(bytes.AsSpan(HeaderSizeBytes));
        }

        return bytes;
    }

    // ---- IPacketPayload ----

    public string PayloadType => "UDP";

    /// <summary>Total simulated size in bytes: the 8-byte header plus the payload - the same value the UDP "Length" field carries.</summary>
    public int Length => HeaderSizeBytes + Payload.Length;

    /// <summary>The carried application data - never another protocol layer. UDP is a leaf transport, like <see cref="IcmpMessage"/>.</summary>
    public IPacketPayload? EncapsulatedPayload => Payload;

    /// <summary>Structural self-check: the payload chain is itself valid. Checksum correctness is a separate, processing-time concern (see <see cref="UdpProcessor"/>).</summary>
    public PacketValidationResult Validate() => Payload.Validate();

    public override string ToString() => $"UDP {SourcePort} -> {DestinationPort} {Length}B";
}
