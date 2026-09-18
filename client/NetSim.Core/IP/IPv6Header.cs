using System.Globalization;
using NetSim.Core.Common.Exceptions;
using NetSim.Core.Networking;
using NetSim.Core.Packets;

namespace NetSim.Core.IP;

/// <summary>
/// The fixed 40-byte header of an <see cref="IPv6Packet"/>. Immutable; built by
/// <see cref="IPv6Packet"/> (its constructor is <c>internal</c>) so a header's
/// <see cref="PayloadLengthBytes"/> always matches the packet's real extension headers + payload.
///
/// All eight IPv6 header fields are represented: <see cref="Version"/> (fixed at 6),
/// <see cref="TrafficClass"/>, <see cref="FlowLabel"/> (20-bit), <see cref="PayloadLengthBytes"/>,
/// <see cref="NextHeader"/>, <see cref="HopLimit"/> and the source / destination addresses. IPv6
/// has no header checksum, so - unlike IPv4 - there is nothing to compute; <see cref="ToBytes"/> is
/// provided for a future serializer only and the engine never uses it.
/// </summary>
public sealed class IPv6Header
{
    /// <summary>The IP version this header describes. Always 6 - not configurable.</summary>
    public const int Version = 6;

    /// <summary>The size of the fixed IPv6 header, in bytes. IPv6 has no variable-length options in the fixed header.</summary>
    public const int HeaderLengthBytes = 40;

    /// <summary>The largest value the 20-bit Flow Label field can hold.</summary>
    public const int MaxFlowLabel = 0xFFFFF;

    /// <summary>The largest value the 16-bit Payload Length field can hold.</summary>
    public const int MaxPayloadLength = ushort.MaxValue;

    internal IPv6Header(
        IPv6Address sourceAddress,
        IPv6Address destinationAddress,
        NextHeader nextHeader,
        byte hopLimit,
        int payloadLengthBytes,
        byte trafficClass,
        int flowLabel)
    {
        if (flowLabel is < 0 or > MaxFlowLabel)
        {
            throw new DomainException($"IPv6 flow label must be between 0 and {MaxFlowLabel}, but was {flowLabel}.");
        }

        if (payloadLengthBytes < 0)
        {
            throw new DomainException($"IPv6 payload length cannot be negative (was {payloadLengthBytes}).");
        }

        if (payloadLengthBytes > MaxPayloadLength)
        {
            throw new DomainException(
                $"IPv6 payload length ({payloadLengthBytes}) exceeds the 16-bit maximum of {MaxPayloadLength}.");
        }

        SourceAddress = sourceAddress;
        DestinationAddress = destinationAddress;
        NextHeader = nextHeader;
        HopLimit = hopLimit;
        PayloadLengthBytes = payloadLengthBytes;
        TrafficClass = trafficClass;
        FlowLabel = flowLabel;
    }

    /// <summary>Differentiated services / traffic class (0-255). Represented only - no QoS.</summary>
    public byte TrafficClass { get; }

    /// <summary>The 20-bit Flow Label (0-1048575). Represented only - no flow-based QoS or traffic engineering.</summary>
    public int FlowLabel { get; }

    /// <summary>
    /// Length in bytes of everything after this fixed 40-byte header - the extension header chain
    /// plus the upper-layer payload. This is <em>not</em> the whole packet size (which is
    /// <see cref="HeaderLengthBytes"/> + this).
    /// </summary>
    public int PayloadLengthBytes { get; }

    /// <summary>Identifies the first thing after this header - an extension header or an upper-layer protocol.</summary>
    public NextHeader NextHeader { get; }

    /// <summary>Hop Limit - the IPv6 equivalent of the IPv4 TTL. Decremented by a future forwarding step; here it is only modelled.</summary>
    public byte HopLimit { get; }

    /// <summary>The packet's source address.</summary>
    public IPv6Address SourceAddress { get; }

    /// <summary>The packet's destination address.</summary>
    public IPv6Address DestinationAddress { get; }

    /// <summary>
    /// The 40 fixed-header bytes in network byte order. Provided for a future serialization phase;
    /// the engine does not use it. IPv6 has no header checksum.
    /// </summary>
    public byte[] ToBytes()
    {
        var bytes = new byte[HeaderLengthBytes];

        // Version (4 bits) | Traffic Class (8 bits) | Flow Label (20 bits).
        var versionTrafficFlow = ((uint)Version << 28) | ((uint)TrafficClass << 20) | (uint)FlowLabel;
        bytes[0] = (byte)(versionTrafficFlow >> 24);
        bytes[1] = (byte)(versionTrafficFlow >> 16);
        bytes[2] = (byte)(versionTrafficFlow >> 8);
        bytes[3] = (byte)versionTrafficFlow;

        bytes[4] = (byte)(PayloadLengthBytes >> 8);
        bytes[5] = (byte)PayloadLengthBytes;
        bytes[6] = NextHeader.Value;
        bytes[7] = HopLimit;

        SourceAddress.GetBytes().CopyTo(bytes, 8);
        DestinationAddress.GetBytes().CopyTo(bytes, 24);

        return bytes;
    }

    /// <summary>
    /// Structural self-check: the flow label and payload length are in range and the destination
    /// address is set. No protocol semantics - what a given <see cref="NextHeader"/> means is a
    /// later phase's concern.
    /// </summary>
    public PacketValidationResult Validate()
    {
        var errors = new List<string>();

        if (FlowLabel is < 0 or > MaxFlowLabel)
        {
            errors.Add($"Flow label {FlowLabel} is outside 0-{MaxFlowLabel}.");
        }

        if (PayloadLengthBytes is < 0 or > MaxPayloadLength)
        {
            errors.Add($"Payload length {PayloadLengthBytes} is outside 0-{MaxPayloadLength}.");
        }

        if (DestinationAddress.IsUnspecified)
        {
            errors.Add("Destination address is not set ('::').");
        }

        if (SourceAddress.IsMulticast)
        {
            errors.Add($"Source address '{SourceAddress}' is a multicast address and cannot be a packet source.");
        }

        return errors.Count == 0 ? PacketValidationResult.Valid : new PacketValidationResult(errors);
    }

    public override string ToString() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"IPv6 {SourceAddress} -> {DestinationAddress} next={NextHeader.Name} hop={HopLimit} plen={PayloadLengthBytes}");
}
