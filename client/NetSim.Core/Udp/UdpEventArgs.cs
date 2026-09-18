using NetSim.Core.IP;
using NetSim.Core.Networking;
using NetSim.Core.Packets;

namespace NetSim.Core.Udp;

/// <summary>
/// Payload for every <see cref="IUdpLayer"/> notification - the same Core-stays-dispatch-agnostic
/// carrier pattern as <see cref="Icmp.IcmpEventArgs"/>: every field is optional, so one type serves
/// created / encapsulated / delivered / dropped / port-unavailable events.
/// </summary>
public sealed class UdpEventArgs : EventArgs
{
    public UdpEventArgs(
        UdpDatagram? datagram = null,
        IPv4Packet? ipPacket = null,
        Packet? carrier = null,
        NetworkInterface? networkInterface = null,
        PacketDropReason? dropReason = null,
        string? detail = null)
    {
        Datagram = datagram;
        IPv4Packet = ipPacket;
        Carrier = carrier;
        Interface = networkInterface;
        DropReason = dropReason;
        Detail = detail;
    }

    public UdpDatagram? Datagram { get; }

    public IPv4Packet? IPv4Packet { get; }

    public Packet? Carrier { get; }

    public NetworkInterface? Interface { get; }

    public PacketDropReason? DropReason { get; }

    public string? Detail { get; }
}
