using NetSim.Core.Devices;
using NetSim.Core.IP;
using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Tcp;
using NetSim.Core.Transport;

namespace NetSim.Core.Tests.Tcp;

public class TcpLayerTests
{
    private static readonly IPv4Address ClientIp = IPv4Address.Parse("192.168.1.10");
    private static readonly IPv4Address ServerIp = IPv4Address.Parse("192.168.1.20");
    private static readonly Port ServerPort = Port.Create(80);

    private static (TcpLayer Layer, ITcpConnectionManager Manager, NetworkInterface ServerInterface) BuildLab()
    {
        var manager = new TcpConnectionManager(new SequentialInitialSequenceNumberGenerator());
        var layer = new TcpLayer(new TcpProcessor(), manager);
        var device = new Pc("Server");
        var iface = device.AddInterface("Eth0", InterfaceType.FastEthernet);
        iface.AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(ServerIp, 24));
        return (layer, manager, iface);
    }

    [Fact]
    public void CreateSegment_RaisesSegmentCreated()
    {
        var (layer, _, _) = BuildLab();
        TcpEventArgs? raised = null;
        layer.SegmentCreated += (_, e) => raised = e;

        var segment = layer.CreateSegment(ClientIp, ServerIp, Port.Create(1), ServerPort, 1, 0, TcpFlags.Syn);

        Assert.NotNull(raised);
        Assert.Same(segment, raised!.Segment);
    }

    [Fact]
    public void Encapsulate_WrapsInIPv4WithTcpProtocol()
    {
        var (layer, _, _) = BuildLab();
        var segment = layer.CreateSegment(ClientIp, ServerIp, Port.Create(1), ServerPort, 1, 0, TcpFlags.Syn);

        var ip = layer.Encapsulate(segment, ClientIp, ServerIp);

        Assert.Equal(ProtocolNumber.Tcp, ip.Protocol);
        Assert.Same(segment, ip.Payload);
    }

    [Fact]
    public void TryDecapsulate_NonTcpPacket_ReturnsFalse()
    {
        var (layer, _, _) = BuildLab();
        var ip = IPv4Packet.Create(ClientIp, ServerIp, RawPayload.Empty, ProtocolNumber.Udp);

        Assert.False(layer.TryDecapsulate(ip, out _));
    }

    [Fact]
    public void HandleIncoming_SynToAListeningPort_ProducesSynAck()
    {
        var (layer, manager, iface) = BuildLab();
        manager.Listen(iface.Device, ServerPort);
        var syn = layer.CreateSegment(ClientIp, ServerIp, Port.Create(50000), ServerPort, 1000, 0, TcpFlags.Syn);
        var ip = layer.Encapsulate(syn, ClientIp, ServerIp);

        var report = layer.HandleIncoming(iface, ip);

        Assert.Equal(TcpProcessingOutcomeKind.SynReceived, report.Kind);
        Assert.True(report.ResponseSegment!.IsSynAck);
    }

    [Fact]
    public void HandleIncoming_SynToANonListeningPort_IsRefused()
    {
        var (layer, _, iface) = BuildLab();
        var syn = layer.CreateSegment(ClientIp, ServerIp, Port.Create(50000), ServerPort, 1000, 0, TcpFlags.Syn);
        var ip = layer.Encapsulate(syn, ClientIp, ServerIp);

        var report = layer.HandleIncoming(iface, ip);

        Assert.Equal(TcpProcessingOutcomeKind.ConnectionRefused, report.Kind);
        Assert.True(report.ResponseSegment!.IsRst);
    }

    [Fact]
    public void HandleIncoming_ToAnUnownedAddress_IsDropped()
    {
        var (layer, manager, iface) = BuildLab();
        manager.Listen(iface.Device, ServerPort);
        var elsewhere = IPv4Address.Parse("192.168.1.99");
        var syn = layer.CreateSegment(ClientIp, elsewhere, Port.Create(50000), ServerPort, 1000, 0, TcpFlags.Syn);
        var ip = layer.Encapsulate(syn, ClientIp, elsewhere);

        var report = layer.HandleIncoming(iface, ip);

        Assert.False(report.IsSuccess);
        Assert.Same(TcpDropReasons.DestinationNotOwned, report.DropReason);
    }

    [Fact]
    public void HandleIncoming_InvalidChecksum_IsDropped()
    {
        var (layer, manager, iface) = BuildLab();
        manager.Listen(iface.Device, ServerPort);
        var syn = layer.CreateSegment(ClientIp, ServerIp, Port.Create(50000), ServerPort, 1000, 0, TcpFlags.Syn);
        var corrupted = syn.WithChecksum(unchecked((ushort)(syn.Checksum + 1)));
        var ip = layer.Encapsulate(corrupted, ClientIp, ServerIp);
        var dropped = false;
        layer.PacketDropped += (_, _) => dropped = true;

        var report = layer.HandleIncoming(iface, ip);

        Assert.False(report.IsSuccess);
        Assert.Same(TcpDropReasons.InvalidChecksum, report.DropReason);
        Assert.True(dropped);
    }

    [Fact]
    public void HandleIncoming_NotTcp_IsDropped()
    {
        var (layer, _, iface) = BuildLab();
        var ip = IPv4Packet.Create(ClientIp, ServerIp, RawPayload.Empty, ProtocolNumber.Udp);

        var report = layer.HandleIncoming(iface, ip);

        Assert.False(report.IsSuccess);
        Assert.Same(TcpDropReasons.NotTcp, report.DropReason);
    }
}
