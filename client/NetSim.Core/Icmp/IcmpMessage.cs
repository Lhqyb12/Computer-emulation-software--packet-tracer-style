using NetSim.Core.Common.Exceptions;
using NetSim.Core.Packets;

namespace NetSim.Core.Icmp;

/// <summary>
/// A simulated ICMP (IPv4) message. Like <see cref="Arp.ArpPacket"/> it is an
/// <see cref="IPacketPayload"/> that carries nothing further down the protocol stack - it rides as
/// the <see cref="IP.IPv4Packet.Payload"/> of an IPv4 packet whose
/// <see cref="IP.IPv4Header.Protocol"/> is <see cref="IP.ProtocolNumber.Icmp"/>, itself inside an
/// <see cref="Ethernet.EthernetFrame"/> keyed by <see cref="Ethernet.EtherType.IPv4"/> - ICMP never
/// gets its own EtherType. <see cref="EncapsulatedPayload"/> exposes <see cref="Data"/> (the echo
/// payload, e.g. "abcdefgh...") purely so the packet engine's length/validation chain sees it; it
/// is opaque bytes, never another protocol layer.
///
/// Phase 21 models four types (<see cref="IcmpType.EchoRequest"/> / <see cref="IcmpType.EchoReply"/>
/// fully; <see cref="IcmpType.DestinationUnreachable"/> / <see cref="IcmpType.TimeExceeded"/> as a
/// foundation - see <see cref="IcmpOriginalDatagramInfo"/>). Unlike <see cref="IP.IPv4Header"/>'s
/// checksum (decorative - computed on demand, never validated), an ICMP message's
/// <see cref="Checksum"/> IS meaningful: it is computed correctly by every factory and is what
/// <see cref="Icmp.IcmpProcessor"/> checks via <see cref="HasValidChecksum"/> before delivering a
/// message - the brief explicitly asks for checksum validation, not just a decorative field.
/// </summary>
public sealed class IcmpMessage : IPacketPayload
{
    /// <summary>Simulated header size: type(1) + code(1) + checksum(2) + identifier(2) + sequence(2), or the equivalent 4 "unused" bytes for an error message.</summary>
    public const int HeaderSizeBytes = 8;

    private IcmpMessage(
        IcmpType type,
        IcmpCode code,
        ushort identifier,
        ushort sequenceNumber,
        RawPayload data,
        IcmpOriginalDatagramInfo? originalDatagram,
        ushort? checksumOverride)
    {
        Type = type;
        Code = code;
        Identifier = identifier;
        SequenceNumber = sequenceNumber;
        Data = data;
        OriginalDatagram = originalDatagram;
        Checksum = checksumOverride ?? ComputeChecksumInternal(type, code, identifier, sequenceNumber, data, originalDatagram);
    }

    // ---- Factories - an invalid message never exists ----

    /// <summary>Builds an Echo Request (Type 8, Code 0) - what a ping sends. <paramref name="data"/> defaults to an empty payload.</summary>
    public static IcmpMessage CreateEchoRequest(ushort identifier, ushort sequenceNumber, RawPayload? data = null) =>
        new(IcmpType.EchoRequest, IcmpCode.Zero, identifier, sequenceNumber, data ?? RawPayload.Empty, originalDatagram: null, checksumOverride: null);

    /// <summary>Builds an Echo Reply (Type 0, Code 0) with the given identifier/sequence/data.</summary>
    public static IcmpMessage CreateEchoReply(ushort identifier, ushort sequenceNumber, RawPayload? data = null) =>
        new(IcmpType.EchoReply, IcmpCode.Zero, identifier, sequenceNumber, data ?? RawPayload.Empty, originalDatagram: null, checksumOverride: null);

    /// <summary>
    /// Builds the Echo Reply that answers <paramref name="request"/>: same identifier, sequence
    /// number and data (section 10/11 - "preserve the relevant values"). Throws
    /// <see cref="DomainException"/> if <paramref name="request"/> is not an Echo Request.
    /// </summary>
    public static IcmpMessage CreateEchoReplyTo(IcmpMessage request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.IsEchoRequest)
        {
            throw new DomainException($"CreateEchoReplyTo requires an Echo Request, but was {request.Type.Name}.");
        }

        return CreateEchoReply(request.Identifier, request.SequenceNumber, request.Data);
    }

    /// <summary>
    /// Builds a Time Exceeded message (Type 11) about <paramref name="originalDatagram"/> - the
    /// foundation section 28 asks for; nothing in this phase generates one (no forwarding engine
    /// decrements TTL to zero yet).
    /// </summary>
    public static IcmpMessage CreateTimeExceeded(IcmpOriginalDatagramInfo originalDatagram, IcmpCode? code = null)
    {
        ArgumentNullException.ThrowIfNull(originalDatagram);
        return new(IcmpType.TimeExceeded, code ?? IcmpCode.TimeToLiveExceededInTransit, 0, 0, RawPayload.Empty, originalDatagram, checksumOverride: null);
    }

    /// <summary>
    /// Builds a Destination Unreachable message (Type 3) about <paramref name="originalDatagram"/>
    /// with the given reason <paramref name="code"/> - the foundation section 29 asks for; nothing
    /// in this phase generates one (no routing / transport layer yet).
    /// </summary>
    public static IcmpMessage CreateDestinationUnreachable(IcmpOriginalDatagramInfo originalDatagram, IcmpCode code)
    {
        ArgumentNullException.ThrowIfNull(originalDatagram);
        return new(IcmpType.DestinationUnreachable, code, 0, 0, RawPayload.Empty, originalDatagram, checksumOverride: null);
    }

    // ---- Fields ----

    public IcmpType Type { get; }

    public IcmpCode Code { get; }

    /// <summary>Distinguishes one ping "session" from another. Meaningful only for Echo Request/Reply - 0 otherwise.</summary>
    public ushort Identifier { get; }

    /// <summary>Distinguishes one echo within a session from the next. Meaningful only for Echo Request/Reply - 0 otherwise.</summary>
    public ushort SequenceNumber { get; }

    /// <summary>The echo payload (e.g. "abcdefgh..."). Never null - <see cref="RawPayload.Empty"/> for an error message.</summary>
    public RawPayload Data { get; }

    /// <summary>The original datagram this error message is about - set only for <see cref="IsError"/>, otherwise null.</summary>
    public IcmpOriginalDatagramInfo? OriginalDatagram { get; }

    /// <summary>
    /// The RFC 792 checksum, computed correctly at creation by every factory. Use
    /// <see cref="WithChecksum"/> to build a message that simulates transmission corruption.
    /// </summary>
    public ushort Checksum { get; }

    public bool IsEchoRequest => Type == IcmpType.EchoRequest;

    public bool IsEchoReply => Type == IcmpType.EchoReply;

    public bool IsDestinationUnreachable => Type == IcmpType.DestinationUnreachable;

    public bool IsTimeExceeded => Type == IcmpType.TimeExceeded;

    public bool IsError => IsDestinationUnreachable || IsTimeExceeded;

    /// <summary>Recomputes the checksum from the current field values (with the checksum field itself zeroed) - the value a correctly-formed message carries.</summary>
    public ushort ComputeChecksum() => ComputeChecksumInternal(Type, Code, Identifier, SequenceNumber, Data, OriginalDatagram);

    /// <summary>True when <see cref="Checksum"/> matches what the current fields compute to - false for a message corrupted in transit.</summary>
    public bool HasValidChecksum => Checksum == ComputeChecksum();

    /// <summary>
    /// Returns a copy of this message carrying <paramref name="checksum"/> instead of the correct
    /// one. Simulates a message corrupted in transit, for fault-injection scenarios and tests -
    /// mirrors <see cref="IP.IPv4Packet.WithDecrementedTimeToLive"/> ("return a modified copy").
    /// </summary>
    public IcmpMessage WithChecksum(ushort checksum) =>
        new(Type, Code, Identifier, SequenceNumber, Data, OriginalDatagram, checksumOverride: checksum);

    private static ushort ComputeChecksumInternal(
        IcmpType type, IcmpCode code, ushort identifier, ushort sequenceNumber, RawPayload data, IcmpOriginalDatagramInfo? originalDatagram) =>
        IcmpChecksum.Compute(ToBytesCore(type, code, identifier, sequenceNumber, data, originalDatagram));

    /// <summary>
    /// The message's bytes in network byte order, with the checksum field zeroed - provided for
    /// checksum computation and a future serialisation phase, mirroring
    /// <see cref="IP.IPv4Header.ToBytes"/>. Not consulted anywhere else on the hot path.
    /// </summary>
    public byte[] ToBytes() => ToBytesCore(Type, Code, Identifier, SequenceNumber, Data, OriginalDatagram);

    private static byte[] ToBytesCore(
        IcmpType type, IcmpCode code, ushort identifier, ushort sequenceNumber, RawPayload data, IcmpOriginalDatagramInfo? originalDatagram)
    {
        var dataSpan = data.Data.Span;
        var bytes = new byte[HeaderSizeBytes + dataSpan.Length + (originalDatagram is null ? 0 : IcmpOriginalDatagramInfo.SimulatedLengthBytes)];

        bytes[0] = type.Value;
        bytes[1] = code.Value;
        // bytes[2..3] = checksum, left zero.

        if (type == IcmpType.EchoRequest || type == IcmpType.EchoReply)
        {
            bytes[4] = (byte)(identifier >> 8);
            bytes[5] = (byte)(identifier & 0xFF);
            bytes[6] = (byte)(sequenceNumber >> 8);
            bytes[7] = (byte)(sequenceNumber & 0xFF);
        }
        // else bytes[4..7] stay 0 ("unused"/pointer field this behavioural model does not populate).

        dataSpan.CopyTo(bytes.AsSpan(HeaderSizeBytes));
        // The trailing OriginalDatagram bytes (if any) stay zero - a nominal size contribution only,
        // consistent with this codebase modelling behaviour rather than a byte-exact wire format.

        return bytes;
    }

    // ---- IPacketPayload ----

    public string PayloadType => "ICMP";

    public int Length => HeaderSizeBytes + Data.Length + (OriginalDatagram is null ? 0 : IcmpOriginalDatagramInfo.SimulatedLengthBytes);

    /// <summary>The echo payload - never another protocol layer. Never null (<see cref="RawPayload.Empty"/> when there is none).</summary>
    public IPacketPayload? EncapsulatedPayload => Data;

    /// <summary>
    /// Structural self-check: the type is one of the four this project knows, Echo Request/Reply
    /// always use code 0, an error message carries its <see cref="OriginalDatagram"/>, and the data
    /// payload is itself valid. Checksum correctness is a separate, processing-time concern (see
    /// <see cref="HasValidChecksum"/> / <see cref="IcmpProcessor"/>) - not part of structural
    /// validity, exactly like every other protocol layer in this codebase.
    /// </summary>
    public PacketValidationResult Validate()
    {
        var errors = new List<string>();

        if (!Type.IsKnown)
        {
            errors.Add($"Unsupported ICMP type {Type.Value}.");
        }
        else if (IsEchoRequest || IsEchoReply)
        {
            if (Code.Value != 0)
            {
                errors.Add($"ICMP {Type.Name} must use code 0, but was {Code.Value}.");
            }
        }
        else if (IsError && OriginalDatagram is null)
        {
            errors.Add($"ICMP {Type.Name} must carry the original datagram information.");
        }

        var dataResult = Data.Validate();
        if (!dataResult.IsValid)
        {
            errors.AddRange(dataResult.Errors);
        }

        return errors.Count == 0 ? PacketValidationResult.Valid : new PacketValidationResult(errors);
    }

    public override string ToString() => Type.Value switch
    {
        8 => $"ICMP Echo Request id={Identifier} seq={SequenceNumber} {Data.Length}B",
        0 => $"ICMP Echo Reply id={Identifier} seq={SequenceNumber} {Data.Length}B",
        3 => $"ICMP Destination Unreachable ({Code.NameFor(Type)}) for {OriginalDatagram}",
        11 => $"ICMP Time Exceeded ({Code.NameFor(Type)}) for {OriginalDatagram}",
        _ => $"ICMP {Type.Name} code={Code.Value}",
    };
}
