using NetSim.Core.Packets;

namespace NetSim.Core.Tcp;

/// <summary>The TCP engine's catalogue of structured drop reasons - the same per-layer catalogue pattern as <see cref="Udp.UdpDropReasons"/> / <see cref="Icmp.IcmpDropReasons"/>.</summary>
public static class TcpDropReasons
{
    /// <summary>The packet does not carry a TCP segment wrapped in an IPv4 packet.</summary>
    public static PacketDropReason NotTcp { get; } = new("TCP_NOT_TCP", "The packet does not carry a TCP segment.");

    /// <summary>The TCP segment (or its payload, or its flag combination) failed structural validation.</summary>
    public static PacketDropReason InvalidPacket { get; } = new("TCP_INVALID_PACKET", "The TCP segment failed structural validation.");

    /// <summary>The TCP checksum does not match the segment's contents/addresses - it was corrupted in transit.</summary>
    public static PacketDropReason InvalidChecksum { get; } = new("TCP_INVALID_CHECKSUM", "The TCP checksum does not match the segment contents.");

    /// <summary>The receiving interface does not own the segment's IPv4 destination address - never forwarded, no routing.</summary>
    public static PacketDropReason DestinationNotOwned { get; } = new("TCP_DESTINATION_NOT_OWNED", "The receiving interface does not own the destination address.");

    /// <summary>No listener exists for the destination port and no matching connection exists - the connection was refused.</summary>
    public static PacketDropReason ConnectionRefused { get; } = new("TCP_CONNECTION_REFUSED", "No listener or matching connection exists for this segment.");

    /// <summary>A segment arrived for a connection whose current state cannot handle it (e.g. data on a closed connection).</summary>
    public static PacketDropReason InvalidConnectionState { get; } = new("TCP_INVALID_CONNECTION_STATE", "The segment is not valid for the connection's current state.");

    /// <summary>A data or FIN segment's sequence number does not match what the connection expects next.</summary>
    public static PacketDropReason UnexpectedSequenceNumber { get; } = new("TCP_UNEXPECTED_SEQUENCE_NUMBER", "The segment's sequence number does not match the connection's expected next sequence number.");
}
