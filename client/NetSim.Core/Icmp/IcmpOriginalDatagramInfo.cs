using NetSim.Core.IP;
using NetSim.Core.Networking;

namespace NetSim.Core.Icmp;

/// <summary>
/// The identifying information an ICMP error message (<see cref="IcmpType.DestinationUnreachable"/>
/// / <see cref="IcmpType.TimeExceeded"/>) carries about the IPv4 datagram that triggered it - the
/// foundation the brief asks for (section 30) so a later phase can build Traceroute / richer error
/// reporting without redesigning <see cref="IcmpMessage"/>. A real ICMP error message embeds the
/// original IPv4 header plus the first 8 payload bytes; this behavioural model keeps just the
/// fields that identify the datagram (source, destination, protocol, TTL) rather than a byte-exact
/// copy, consistent with the rest of this codebase not being a wire-format codec.
/// </summary>
public sealed class IcmpOriginalDatagramInfo
{
    /// <summary>Nominal size contribution to <see cref="IcmpMessage.Length"/>: a 20-byte IPv4 header plus the first 8 payload bytes (RFC 792).</summary>
    public const int SimulatedLengthBytes = 28;

    private IcmpOriginalDatagramInfo(IPv4Address sourceAddress, IPv4Address destinationAddress, ProtocolNumber protocol, byte timeToLive)
    {
        SourceAddress = sourceAddress;
        DestinationAddress = destinationAddress;
        Protocol = protocol;
        TimeToLive = timeToLive;
    }

    /// <summary>The original datagram's source address.</summary>
    public IPv4Address SourceAddress { get; }

    /// <summary>The original datagram's destination address.</summary>
    public IPv4Address DestinationAddress { get; }

    /// <summary>The original datagram's protocol.</summary>
    public ProtocolNumber Protocol { get; }

    /// <summary>The original datagram's TTL at the point the error was generated.</summary>
    public byte TimeToLive { get; }

    /// <summary>Captures the identifying fields of <paramref name="originalPacket"/>.</summary>
    public static IcmpOriginalDatagramInfo FromPacket(IPv4Packet originalPacket)
    {
        ArgumentNullException.ThrowIfNull(originalPacket);
        return new IcmpOriginalDatagramInfo(
            originalPacket.SourceAddress, originalPacket.DestinationAddress, originalPacket.Protocol, originalPacket.TimeToLive);
    }

    public override string ToString() => $"{SourceAddress} -> {DestinationAddress} [{Protocol.Name}] ttl={TimeToLive}";
}
