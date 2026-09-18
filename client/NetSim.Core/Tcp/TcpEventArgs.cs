using NetSim.Core.IP;
using NetSim.Core.Networking;
using NetSim.Core.Packets;

namespace NetSim.Core.Tcp;

/// <summary>
/// Payload for every <see cref="ITcpLayer"/> notification (segment-level, protocol-mechanics
/// events) - the same optional-fields carrier pattern as <see cref="Icmp.IcmpEventArgs"/> /
/// <see cref="Udp.UdpEventArgs"/>. Connection-lifecycle events use <see cref="TcpConnectionEventArgs"/> instead.
/// </summary>
public sealed class TcpEventArgs : EventArgs
{
    public TcpEventArgs(
        TcpSegment? segment = null,
        IPv4Packet? ipPacket = null,
        Packet? carrier = null,
        NetworkInterface? networkInterface = null,
        PacketDropReason? dropReason = null,
        string? detail = null)
    {
        Segment = segment;
        IPv4Packet = ipPacket;
        Carrier = carrier;
        Interface = networkInterface;
        DropReason = dropReason;
        Detail = detail;
    }

    public TcpSegment? Segment { get; }

    public IPv4Packet? IPv4Packet { get; }

    public Packet? Carrier { get; }

    public NetworkInterface? Interface { get; }

    public PacketDropReason? DropReason { get; }

    public string? Detail { get; }
}
