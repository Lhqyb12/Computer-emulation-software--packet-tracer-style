using NetSim.Core.Devices;
using NetSim.Core.IP;
using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Transport;
using NetSim.Core.Udp;

namespace NetSim.Core.Tests.Udp;

public class UdpLayerTests
{
    private static readonly IPv4Address SourceIp = IPv4Address.Parse("192.168.1.10");
    private static readonly IPv4Address DestinationIp = IPv4Address.Parse("192.168.1.20");

    private static (UdpLayer Layer, UdpDeliveryManager Delivery, NetworkInterface Interface) BuildLab()
    {
        var delivery = new UdpDeliveryManager();
        var layer = new UdpLayer(new UdpProcessor(), delivery);
        var device = new Pc("Server");
        var iface = device.AddInterface("Eth0", InterfaceType.FastEthernet);
        iface.AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(DestinationIp, 24));
        return (layer, delivery, iface);
    }

    [Fact]
    public void CreateDatagram_RaisesDatagramCreated()
    {
        var (layer, _, _) = BuildLab();
        UdpEventArgs? raised = null;
        layer.DatagramCreated += (_, e) => raised = e;

        var datagram = layer.CreateDatagram(SourceIp, DestinationIp, Port.Create(1), Port.Create(2));

        Assert.NotNull(raised);
        Assert.Same(datagram, raised!.Datagram);
    }

    [Fact]
    public void Encapsulate_WrapsInIPv4WithUdpProtocol()
    {
        var (layer, _, _) = BuildLab();
        var datagram = layer.CreateDatagram(SourceIp, DestinationIp, Port.Create(1), Port.Create(2));

        var ip = layer.Encapsulate(datagram, SourceIp, DestinationIp);

        Assert.Equal(ProtocolNumber.Udp, ip.Protocol);
        Assert.Same(datagram, ip.Payload);
    }

    [Fact]
    public void TryDecapsulate_NonUdpPacket_ReturnsFalse()
    {
        var (layer, _, _) = BuildLab();
        var ip = IPv4Packet.Create(SourceIp, DestinationIp, RawPayload.Empty, ProtocolNumber.Tcp);

        Assert.False(layer.TryDecapsulate(ip, out _));
    }

    [Fact]
    public void HandleIncoming_ToABoundPort_Delivers()
    {
        var (layer, delivery, iface) = BuildLab();
        delivery.Bind(iface.Device, Port.Create(5000));
        var datagram = layer.CreateDatagram(SourceIp, DestinationIp, Port.Create(50000), Port.Create(5000), RawPayload.FromText("Hello"));
        var ip = layer.Encapsulate(datagram, SourceIp, DestinationIp);

        var report = layer.HandleIncoming(iface, ip);

        Assert.True(report.IsSuccess);
        Assert.True(report.IsDelivered);
        Assert.True(report.LocalInterfaceOwnsDestination);
    }

    [Fact]
    public void HandleIncoming_ToAnUnboundPort_RaisesPortUnavailable()
    {
        var (layer, _, iface) = BuildLab();
        var datagram = layer.CreateDatagram(SourceIp, DestinationIp, Port.Create(50000), Port.Create(9999));
        var ip = layer.Encapsulate(datagram, SourceIp, DestinationIp);
        var raised = false;
        layer.PortUnavailable += (_, _) => raised = true;

        var report = layer.HandleIncoming(iface, ip);

        Assert.True(report.IsSuccess);
        Assert.False(report.IsDelivered);
        Assert.True(raised);
    }

    [Fact]
    public void HandleIncoming_ToAnUnownedAddress_IsNotDelivered()
    {
        var (layer, delivery, iface) = BuildLab();
        delivery.Bind(iface.Device, Port.Create(5000));
        var elsewhere = IPv4Address.Parse("192.168.1.99");
        var datagram = layer.CreateDatagram(SourceIp, elsewhere, Port.Create(50000), Port.Create(5000));
        var ip = layer.Encapsulate(datagram, SourceIp, elsewhere);

        var report = layer.HandleIncoming(iface, ip);

        Assert.True(report.IsSuccess);
        Assert.False(report.LocalInterfaceOwnsDestination);
        Assert.False(report.IsDelivered);
    }

    [Fact]
    public void HandleIncoming_InvalidChecksum_IsDropped()
    {
        var (layer, delivery, iface) = BuildLab();
        delivery.Bind(iface.Device, Port.Create(5000));
        var datagram = layer.CreateDatagram(SourceIp, DestinationIp, Port.Create(50000), Port.Create(5000));
        var corrupted = datagram.WithChecksum(unchecked((ushort)(datagram.Checksum + 1)));
        var ip = layer.Encapsulate(corrupted, SourceIp, DestinationIp);
        var dropped = false;
        layer.PacketDropped += (_, _) => dropped = true;

        var report = layer.HandleIncoming(iface, ip);

        Assert.False(report.IsSuccess);
        Assert.Same(UdpDropReasons.InvalidChecksum, report.DropReason);
        Assert.True(dropped);
    }

    [Fact]
    public void HandleIncoming_NotUdp_IsDropped()
    {
        var (layer, _, iface) = BuildLab();
        var ip = IPv4Packet.Create(SourceIp, DestinationIp, RawPayload.Empty, ProtocolNumber.Tcp);

        var report = layer.HandleIncoming(iface, ip);

        Assert.False(report.IsSuccess);
        Assert.Same(UdpDropReasons.NotUdp, report.DropReason);
    }
}
