using NetSim.Core.Icmp;
using NetSim.Core.IP;
using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Transport;

namespace NetSim.Core.Tcp;

/// <summary>
/// A simulated TCP segment (RFC 793): Source/Destination Port, Sequence/Acknowledgment Number,
/// Flags, Window Size, Checksum, Urgent Pointer and an application payload. Like
/// <see cref="Udp.UdpDatagram"/> it is a leaf <see cref="IPacketPayload"/> and rides as the
/// <see cref="IPv4Packet.Payload"/> of an IPv4 packet whose <see cref="IPv4Header.Protocol"/> is
/// <see cref="ProtocolNumber.Tcp"/> (6).
///
/// This is a behavioural model, not a byte-exact wire format: <see cref="HeaderSizeBytes"/> is the
/// fixed 20-byte header with no options (brief section 9: "does not need to implement every advanced
/// TCP option"); <see cref="DataOffsetWords"/> is reported as a constant 5 (20 bytes / 4) purely for
/// diagnostic completeness. The checksum uses the same IPv4-pseudo-header algorithm as UDP (see
/// <see cref="TransportChecksum"/>), so - like <see cref="Udp.UdpDatagram"/> - the source/destination
/// addresses must be supplied at creation even though neither is itself a header field.
/// </summary>
public sealed class TcpSegment : IPacketPayload
{
    /// <summary>Simulated fixed header size: ports(4) + seq(4) + ack(4) + offset/flags(2) + window(2) + checksum(2) + urgent(2). No options.</summary>
    public const int HeaderSizeBytes = 20;

    /// <summary><see cref="HeaderSizeBytes"/> expressed in 32-bit words - what a real TCP header's Data Offset field would carry.</summary>
    public const int DataOffsetWords = HeaderSizeBytes / 4;

    /// <summary>A nominal default advertised window - this phase does not model flow control / congestion, so the value is never consulted.</summary>
    public const ushort DefaultWindowSize = 65535;

    private TcpSegment(
        Port sourcePort, Port destinationPort, uint sequenceNumber, uint acknowledgmentNumber,
        TcpFlags flags, ushort windowSize, ushort urgentPointer, IPacketPayload payload, ushort checksumOverride)
    {
        SourcePort = sourcePort;
        DestinationPort = destinationPort;
        SequenceNumber = sequenceNumber;
        AcknowledgmentNumber = acknowledgmentNumber;
        Flags = flags;
        WindowSize = windowSize;
        UrgentPointer = urgentPointer;
        Payload = payload;
        Checksum = checksumOverride;
    }

    /// <summary>Builds a segment with a correctly-computed checksum for the given source/destination addresses. An invalid segment never exists.</summary>
    public static TcpSegment Create(
        IPv4Address source, IPv4Address destination, Port sourcePort, Port destinationPort,
        uint sequenceNumber, uint acknowledgmentNumber, TcpFlags flags,
        ushort windowSize = DefaultWindowSize, IPacketPayload? payload = null, ushort urgentPointer = 0)
    {
        var body = payload ?? RawPayload.Empty;
        var checksum = ComputeChecksumInternal(
            source, destination, sourcePort, destinationPort, sequenceNumber, acknowledgmentNumber, flags, windowSize, urgentPointer, body, 0);
        return new TcpSegment(sourcePort, destinationPort, sequenceNumber, acknowledgmentNumber, flags, windowSize, urgentPointer, body, checksum);
    }

    // ---- Named convenience factories (brief section 12: "SYN", "SYN + ACK", "ACK", "FIN + ACK", "RST") ----

    public static TcpSegment CreateSyn(
        IPv4Address source, IPv4Address destination, Port sourcePort, Port destinationPort, uint initialSequenceNumber, ushort windowSize = DefaultWindowSize) =>
        Create(source, destination, sourcePort, destinationPort, initialSequenceNumber, 0, TcpFlags.Syn, windowSize);

    public static TcpSegment CreateSynAck(
        IPv4Address source, IPv4Address destination, Port sourcePort, Port destinationPort,
        uint initialSequenceNumber, uint acknowledgmentNumber, ushort windowSize = DefaultWindowSize) =>
        Create(source, destination, sourcePort, destinationPort, initialSequenceNumber, acknowledgmentNumber, TcpFlags.Syn | TcpFlags.Ack, windowSize);

    public static TcpSegment CreateAck(
        IPv4Address source, IPv4Address destination, Port sourcePort, Port destinationPort,
        uint sequenceNumber, uint acknowledgmentNumber, ushort windowSize = DefaultWindowSize, IPacketPayload? payload = null) =>
        Create(source, destination, sourcePort, destinationPort, sequenceNumber, acknowledgmentNumber, TcpFlags.Ack, windowSize, payload);

    public static TcpSegment CreatePushAck(
        IPv4Address source, IPv4Address destination, Port sourcePort, Port destinationPort,
        uint sequenceNumber, uint acknowledgmentNumber, IPacketPayload payload, ushort windowSize = DefaultWindowSize) =>
        Create(source, destination, sourcePort, destinationPort, sequenceNumber, acknowledgmentNumber, TcpFlags.Psh | TcpFlags.Ack, windowSize, payload);

    public static TcpSegment CreateFin(
        IPv4Address source, IPv4Address destination, Port sourcePort, Port destinationPort,
        uint sequenceNumber, uint acknowledgmentNumber, ushort windowSize = DefaultWindowSize) =>
        Create(source, destination, sourcePort, destinationPort, sequenceNumber, acknowledgmentNumber, TcpFlags.Fin | TcpFlags.Ack, windowSize);

    public static TcpSegment CreateReset(
        IPv4Address source, IPv4Address destination, Port sourcePort, Port destinationPort, uint sequenceNumber, uint acknowledgmentNumber = 0) =>
        Create(source, destination, sourcePort, destinationPort, sequenceNumber, acknowledgmentNumber, acknowledgmentNumber == 0 ? TcpFlags.Rst : TcpFlags.Rst | TcpFlags.Ack, 0);

    // ---- Fields ----

    public Port SourcePort { get; }

    public Port DestinationPort { get; }

    public uint SequenceNumber { get; }

    /// <summary>Meaningful only when <see cref="TcpFlags.Ack"/> is set - the next sequence number the sender of this segment expects to receive.</summary>
    public uint AcknowledgmentNumber { get; }

    public TcpFlags Flags { get; }

    public ushort WindowSize { get; }

    public ushort UrgentPointer { get; }

    /// <summary>The carried application data - never null (<see cref="RawPayload.Empty"/> for a control segment with no data).</summary>
    public IPacketPayload Payload { get; }

    public ushort Checksum { get; }

    public bool IsSyn => Flags.Has(TcpFlags.Syn);

    public bool IsAck => Flags.Has(TcpFlags.Ack);

    public bool IsFin => Flags.Has(TcpFlags.Fin);

    public bool IsRst => Flags.Has(TcpFlags.Rst);

    public bool IsPsh => Flags.Has(TcpFlags.Psh);

    public bool IsSynAck => IsSyn && IsAck;

    /// <summary>
    /// How many bytes of sequence-number space this segment occupies (brief section 14): the
    /// payload length, plus one each for SYN and FIN (each consumes exactly one sequence number,
    /// even though it carries no data byte).
    /// </summary>
    public int SequenceLength => Payload.Length + (IsSyn ? 1 : 0) + (IsFin ? 1 : 0);

    /// <summary>Recomputes the checksum for the given addresses from the current field values.</summary>
    public ushort ComputeChecksum(IPv4Address source, IPv4Address destination) =>
        ComputeChecksumInternal(source, destination, SourcePort, DestinationPort, SequenceNumber, AcknowledgmentNumber, Flags, WindowSize, UrgentPointer, Payload, 0);

    /// <summary>True when <see cref="Checksum"/> matches what the current fields compute to for the given addresses.</summary>
    public bool HasValidChecksum(IPv4Address source, IPv4Address destination) => Checksum == ComputeChecksum(source, destination);

    /// <summary>Returns a copy carrying <paramref name="checksum"/> instead of the correct one - simulates a segment corrupted in transit, mirroring <see cref="IcmpMessage.WithChecksum"/>.</summary>
    public TcpSegment WithChecksum(ushort checksum) =>
        new(SourcePort, DestinationPort, SequenceNumber, AcknowledgmentNumber, Flags, WindowSize, UrgentPointer, Payload, checksum);

    private static ushort ComputeChecksumInternal(
        IPv4Address source, IPv4Address destination, Port sourcePort, Port destinationPort,
        uint sequenceNumber, uint acknowledgmentNumber, TcpFlags flags, ushort windowSize, ushort urgentPointer, IPacketPayload payload, ushort checksumOverride)
    {
        var bytes = ToBytesCore(sourcePort, destinationPort, sequenceNumber, acknowledgmentNumber, flags, windowSize, urgentPointer, payload, checksumOverride);
        return TransportChecksum.Compute(source, destination, ProtocolNumber.Tcp, bytes);
    }

    private static byte[] ToBytesCore(
        Port sourcePort, Port destinationPort, uint sequenceNumber, uint acknowledgmentNumber,
        TcpFlags flags, ushort windowSize, ushort urgentPointer, IPacketPayload payload, ushort checksum)
    {
        var bytes = new byte[HeaderSizeBytes + payload.Length];

        bytes[0] = (byte)(sourcePort.Value >> 8);
        bytes[1] = (byte)(sourcePort.Value & 0xFF);
        bytes[2] = (byte)(destinationPort.Value >> 8);
        bytes[3] = (byte)(destinationPort.Value & 0xFF);

        bytes[4] = (byte)(sequenceNumber >> 24);
        bytes[5] = (byte)(sequenceNumber >> 16);
        bytes[6] = (byte)(sequenceNumber >> 8);
        bytes[7] = (byte)sequenceNumber;

        bytes[8] = (byte)(acknowledgmentNumber >> 24);
        bytes[9] = (byte)(acknowledgmentNumber >> 16);
        bytes[10] = (byte)(acknowledgmentNumber >> 8);
        bytes[11] = (byte)acknowledgmentNumber;

        bytes[12] = (byte)(DataOffsetWords << 4);
        bytes[13] = (byte)flags;

        bytes[14] = (byte)(windowSize >> 8);
        bytes[15] = (byte)(windowSize & 0xFF);

        bytes[16] = (byte)(checksum >> 8);
        bytes[17] = (byte)(checksum & 0xFF);

        bytes[18] = (byte)(urgentPointer >> 8);
        bytes[19] = (byte)(urgentPointer & 0xFF);

        if (payload is RawPayload raw)
        {
            raw.Data.Span.CopyTo(bytes.AsSpan(HeaderSizeBytes));
        }

        return bytes;
    }

    // ---- IPacketPayload ----

    public string PayloadType => "TCP";

    public int Length => HeaderSizeBytes + Payload.Length;

    /// <summary>The carried application data - never another protocol layer. TCP is a leaf transport, like <see cref="Udp.UdpDatagram"/>.</summary>
    public IPacketPayload? EncapsulatedPayload => Payload;

    /// <summary>
    /// Structural self-check: the flag combination is sane (SYN and RST, or SYN and FIN, are never
    /// both set - real TCP never sends either combination) and the payload chain is itself valid.
    /// Checksum correctness is a separate, processing-time concern (see <see cref="TcpProcessor"/>).
    /// </summary>
    public PacketValidationResult Validate()
    {
        var errors = new List<string>();

        if (IsSyn && IsRst)
        {
            errors.Add("A TCP segment cannot set both SYN and RST.");
        }

        if (IsSyn && IsFin)
        {
            errors.Add("A TCP segment cannot set both SYN and FIN.");
        }

        var payloadResult = Payload.Validate();
        if (!payloadResult.IsValid)
        {
            errors.AddRange(payloadResult.Errors);
        }

        return errors.Count == 0 ? PacketValidationResult.Valid : new PacketValidationResult(errors);
    }

    public override string ToString() =>
        $"TCP {SourcePort} -> {DestinationPort} [{Flags.Describe()}] seq={SequenceNumber} ack={AcknowledgmentNumber} win={WindowSize} {Length}B";
}
