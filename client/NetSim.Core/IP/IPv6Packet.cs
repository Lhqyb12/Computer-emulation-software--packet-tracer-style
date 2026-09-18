using NetSim.Core.Common.Exceptions;
using NetSim.Core.Networking;
using NetSim.Core.Packets;

namespace NetSim.Core.IP;

/// <summary>
/// A simulated IPv6 packet. Like <see cref="IPv4Packet"/> / <see cref="Ethernet.EthernetFrame"/> it
/// is an <see cref="IPacketPayload"/>, so the Phase 16 packet engine carries and tracks it with no
/// IPv6-specific changes, and it nests inside an Ethernet frame whose
/// <see cref="Ethernet.EtherType"/> is <see cref="Ethernet.EtherType.IPv6"/>. Its own
/// <see cref="Payload"/> is the next layer down - a generic <see cref="RawPayload"/> today, a
/// future ICMPv6 message or TCP/UDP segment later, keyed by <see cref="NextHeader"/>.
///
/// IPv4 and IPv6 are two different Layer 3 protocols and this type does not pretend otherwise:
/// there is no shared base with <see cref="IPv4Packet"/>, no network/broadcast concept, a 128-bit
/// address, a Hop Limit rather than a TTL, a Flow Label, and an ordered
/// <see cref="ExtensionHeaders"/> chain (empty in this phase - see <see cref="IPv6ExtensionHeader"/>).
/// The <see cref="Header.PayloadLengthBytes"/> is kept consistent with the real extension headers +
/// payload. This is a behavioural model, not a wire-format codec.
/// </summary>
public sealed class IPv6Packet : IPacketPayload
{
    /// <summary>The Hop Limit a freshly created packet gets unless the caller overrides it.</summary>
    public const byte DefaultHopLimit = 64;

    private static readonly IReadOnlyList<IPv6ExtensionHeader> NoExtensionHeaders = [];

    private IPv6Packet(IPv6Header header, IReadOnlyList<IPv6ExtensionHeader> extensionHeaders, IPacketPayload payload)
    {
        Header = header;
        ExtensionHeaders = extensionHeaders;
        Payload = payload;
    }

    /// <summary>
    /// Builds a packet and rejects a structurally invalid one with a <see cref="DomainException"/>
    /// (the destination must be set, the source must not be a multicast address, the Hop Limit must
    /// be at least 1 at creation) - an invalid packet never exists. The <paramref name="payload"/>'s
    /// own validity is the next layer's concern and is checked by <see cref="Validate"/> / the
    /// packet engine; pass null for a payload-less packet.
    /// </summary>
    public static IPv6Packet Create(
        IPv6Address source,
        IPv6Address destination,
        IPacketPayload? payload = null,
        NextHeader nextHeader = default,
        byte hopLimit = DefaultHopLimit,
        byte trafficClass = 0,
        int flowLabel = 0,
        IReadOnlyList<IPv6ExtensionHeader>? extensionHeaders = null)
    {
        if (destination.IsUnspecified)
        {
            throw new DomainException("IPv6 destination address must be set (not '::').");
        }

        if (source.IsMulticast)
        {
            throw new DomainException($"IPv6 source address '{source}' is a multicast address and cannot be a packet source.");
        }

        if (hopLimit == 0)
        {
            throw new DomainException("IPv6 Hop Limit must be at least 1 when a packet is created.");
        }

        var headers = extensionHeaders is { Count: > 0 } ? extensionHeaders : NoExtensionHeaders;
        var body = payload ?? RawPayload.Empty;
        var payloadLength = headers.Sum(h => h.LengthBytes) + body.Length;

        var header = new IPv6Header(source, destination, nextHeader, hopLimit, payloadLength, trafficClass, flowLabel);
        return new IPv6Packet(header, headers, body);
    }

    /// <summary>The fixed IPv6 header fields.</summary>
    public IPv6Header Header { get; }

    /// <summary>
    /// The extension header chain between the fixed header and the payload, in order. Always empty
    /// in this phase - the type exists so a later phase can populate it without changing this class.
    /// </summary>
    public IReadOnlyList<IPv6ExtensionHeader> ExtensionHeaders { get; }

    /// <summary>The encapsulated next layer - never null (<see cref="RawPayload.Empty"/> when the packet carries nothing).</summary>
    public IPacketPayload Payload { get; }

    // ---- Header field pass-throughs (convenience) ----

    public IPv6Address SourceAddress => Header.SourceAddress;

    public IPv6Address DestinationAddress => Header.DestinationAddress;

    public byte HopLimit => Header.HopLimit;

    public NextHeader NextHeader => Header.NextHeader;

    public byte TrafficClass => Header.TrafficClass;

    public int FlowLabel => Header.FlowLabel;

    /// <summary>Length in bytes of the extension headers + payload after the fixed 40-byte header.</summary>
    public int PayloadLength => Header.PayloadLengthBytes;

    /// <summary>True when the Hop Limit has reached zero and the packet can no longer be forwarded.</summary>
    public bool IsHopLimitExhausted => Header.HopLimit == 0;

    // ---- IPacketPayload ----

    public string PayloadType => "IPv6";

    public int Length => IPv6Header.HeaderLengthBytes + Header.PayloadLengthBytes;

    public IPacketPayload? EncapsulatedPayload => Payload;

    /// <summary>
    /// Structural self-check: the header is internally consistent, every extension header is valid,
    /// the declared payload length equals the extension headers + payload, and the encapsulated
    /// payload chain is itself valid. No protocol semantics.
    /// </summary>
    public PacketValidationResult Validate()
    {
        var errors = new List<string>();

        var headerResult = Header.Validate();
        if (!headerResult.IsValid)
        {
            errors.AddRange(headerResult.Errors);
        }

        foreach (var extensionHeader in ExtensionHeaders)
        {
            var extResult = extensionHeader.Validate();
            if (!extResult.IsValid)
            {
                errors.AddRange(extResult.Errors);
            }
        }

        var expectedPayloadLength = ExtensionHeaders.Sum(h => h.LengthBytes) + Payload.Length;
        if (Header.PayloadLengthBytes != expectedPayloadLength)
        {
            errors.Add(
                $"Payload length {Header.PayloadLengthBytes} does not equal extension headers + payload ({expectedPayloadLength}).");
        }

        var payloadResult = Payload.Validate();
        if (!payloadResult.IsValid)
        {
            errors.AddRange(payloadResult.Errors);
        }

        return errors.Count == 0 ? PacketValidationResult.Valid : new PacketValidationResult(errors);
    }

    /// <summary>
    /// Returns a copy of this packet with its Hop Limit reduced by one. The immutable building block
    /// a future forwarding / routing phase needs; this phase performs no forwarding itself. Throws
    /// <see cref="DomainException"/> if the Hop Limit is already 0.
    /// </summary>
    public IPv6Packet WithDecrementedHopLimit()
    {
        if (Header.HopLimit == 0)
        {
            throw new DomainException("Cannot decrement an IPv6 Hop Limit that is already 0.");
        }

        var header = new IPv6Header(
            Header.SourceAddress,
            Header.DestinationAddress,
            Header.NextHeader,
            (byte)(Header.HopLimit - 1),
            Header.PayloadLengthBytes,
            Header.TrafficClass,
            Header.FlowLabel);

        return new IPv6Packet(header, ExtensionHeaders, Payload);
    }

    public override string ToString() =>
        $"{SourceAddress} -> {DestinationAddress} [{NextHeader.Name}] hop={HopLimit} {Length}B";
}
