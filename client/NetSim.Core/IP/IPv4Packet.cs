using NetSim.Core.Common.Exceptions;
using NetSim.Core.Networking;
using NetSim.Core.Packets;

namespace NetSim.Core.IP;

/// <summary>
/// A simulated IPv4 packet. Like <see cref="Ethernet.EthernetFrame"/> it is an
/// <see cref="IPacketPayload"/>, so the Phase 16 packet engine carries and tracks it with no
/// IPv4-specific changes, and it nests inside an Ethernet frame whose
/// <see cref="Ethernet.EtherType"/> is <see cref="Ethernet.EtherType.IPv4"/>. Its own
/// <see cref="Payload"/> is the next layer down - a generic <see cref="RawPayload"/> today, a
/// future ICMP message or TCP/UDP segment later, keyed by <see cref="ProtocolNumber"/>.
///
/// The <see cref="Header"/> holds the fields; the packet keeps them consistent (total length
/// always equals header + payload). This is a behavioural model, not a wire-format codec: there is
/// no binary serialisation on the hot path and no stored checksum (see <see cref="IPv4Checksum"/>).
/// </summary>
public sealed class IPv4Packet : IPacketPayload
{
    /// <summary>The TTL a freshly created packet gets unless the caller overrides it.</summary>
    public const byte DefaultTimeToLive = 64;

    private IPv4Packet(IPv4Header header, IPacketPayload payload)
    {
        Header = header;
        Payload = payload;
    }

    /// <summary>
    /// Builds a packet and rejects a structurally invalid one with a <see cref="DomainException"/>
    /// (the destination must be set, the source must be a plausible host address, the TTL must be
    /// at least 1 at creation) - an invalid packet never exists. The <paramref name="payload"/>'s
    /// own validity is the next layer's concern and is checked by <see cref="Validate"/> / the
    /// packet engine; pass null for a payload-less packet.
    /// </summary>
    public static IPv4Packet Create(
        IPv4Address source,
        IPv4Address destination,
        IPacketPayload? payload = null,
        ProtocolNumber protocol = default,
        byte timeToLive = DefaultTimeToLive,
        ushort identification = 0,
        IPv4Flags flags = IPv4Flags.None,
        int fragmentOffset = 0,
        byte dscp = 0,
        byte ecn = 0)
    {
        if (destination.IsUnspecified)
        {
            throw new DomainException("IPv4 destination address must be set (not 0.0.0.0).");
        }

        if (source.IsLimitedBroadcast || source.IsMulticast)
        {
            throw new DomainException($"IPv4 source address '{source}' is not a valid host source address.");
        }

        if (timeToLive == 0)
        {
            throw new DomainException("IPv4 TTL must be at least 1 when a packet is created.");
        }

        var body = payload ?? RawPayload.Empty;
        var totalLength = IPv4Header.MinimumHeaderLengthBytes + body.Length;
        var header = new IPv4Header(
            source, destination, protocol, timeToLive, totalLength, identification, flags, fragmentOffset, dscp, ecn);

        return new IPv4Packet(header, body);
    }

    /// <summary>The IPv4 header fields.</summary>
    public IPv4Header Header { get; }

    /// <summary>The encapsulated next layer - never null (<see cref="RawPayload.Empty"/> when the packet carries nothing).</summary>
    public IPacketPayload Payload { get; }

    // ---- Header field pass-throughs (convenience) ----

    public IPv4Address SourceAddress => Header.SourceAddress;

    public IPv4Address DestinationAddress => Header.DestinationAddress;

    public byte TimeToLive => Header.TimeToLive;

    public ProtocolNumber Protocol => Header.Protocol;

    public ushort Identification => Header.Identification;

    public IPv4Flags Flags => Header.Flags;

    public int FragmentOffset => Header.FragmentOffset;

    /// <summary>Total packet length in bytes: header + payload.</summary>
    public int TotalLength => Header.TotalLengthBytes;

    /// <summary>True when the TTL has reached zero and the packet can no longer be forwarded.</summary>
    public bool IsTimeToLiveExhausted => Header.TimeToLive == 0;

    // ---- IPacketPayload ----

    public string PayloadType => "IPv4";

    public int Length => Header.HeaderLengthBytes + Payload.Length;

    public IPacketPayload? EncapsulatedPayload => Payload;

    /// <summary>
    /// Structural self-check: the header is internally consistent, the total length equals header +
    /// payload, and the encapsulated payload chain is itself valid. No protocol semantics - what a
    /// given <see cref="ProtocolNumber"/> means is a later phase's concern.
    /// </summary>
    public PacketValidationResult Validate()
    {
        var errors = new List<string>();

        var headerResult = Header.Validate();
        if (!headerResult.IsValid)
        {
            errors.AddRange(headerResult.Errors);
        }

        if (Header.TotalLengthBytes != Header.HeaderLengthBytes + Payload.Length)
        {
            errors.Add(
                $"Total length {Header.TotalLengthBytes} does not equal header ({Header.HeaderLengthBytes}) + payload ({Payload.Length}).");
        }

        var payloadResult = Payload.Validate();
        if (!payloadResult.IsValid)
        {
            errors.AddRange(payloadResult.Errors);
        }

        return errors.Count == 0 ? PacketValidationResult.Valid : new PacketValidationResult(errors);
    }

    /// <summary>
    /// Returns a copy of this packet with its TTL reduced by one. The immutable building block a
    /// future forwarding / routing phase needs; this phase performs no forwarding itself. Throws
    /// <see cref="DomainException"/> if the TTL is already 0.
    /// </summary>
    public IPv4Packet WithDecrementedTimeToLive()
    {
        if (Header.TimeToLive == 0)
        {
            throw new DomainException("Cannot decrement an IPv4 TTL that is already 0.");
        }

        var header = new IPv4Header(
            Header.SourceAddress,
            Header.DestinationAddress,
            Header.Protocol,
            (byte)(Header.TimeToLive - 1),
            Header.TotalLengthBytes,
            Header.Identification,
            Header.Flags,
            Header.FragmentOffset,
            Header.Dscp,
            Header.Ecn);

        return new IPv4Packet(header, Payload);
    }

    public override string ToString() =>
        $"{SourceAddress} -> {DestinationAddress} [{Protocol.Name}] ttl={TimeToLive} {TotalLength}B";
}
