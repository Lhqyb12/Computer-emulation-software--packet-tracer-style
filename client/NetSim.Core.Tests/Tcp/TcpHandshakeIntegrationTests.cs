using System.Linq;
using System.Text;
using NetSim.Core.Arp;
using NetSim.Core.Devices;
using NetSim.Core.Ethernet;
using NetSim.Core.IP;
using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Tcp;
using NetSim.Core.Topology;
using NetSim.Core.Transport;

namespace NetSim.Core.Tests.Tcp;

/// <summary>
/// Phase 22 integration (brief sections 38 &amp; 41): the full "PC0 connects to PC1:80" flow -
/// SYN -&gt; SYN+ACK -&gt; ACK -&gt; Established -&gt; data -&gt; ACK -&gt; FIN/ACK -&gt; Closed - driven through
/// the real Ethernet/ARP/IPv4/TCP engines with every abstraction left intact, mirroring
/// <c>IcmpEchoIntegrationTests</c> / <c>UdpIntegrationTests</c>.
/// </summary>
public class TcpHandshakeIntegrationTests
{
    private static readonly IPv4Address Pc0Ip = IPv4Address.Parse("192.168.1.10");
    private static readonly IPv4Address Pc1Ip = IPv4Address.Parse("192.168.1.20");
    private static readonly Port ClientPort = Port.Create(50000);
    private static readonly Port ServerPort = Port.Create(80);

    private sealed record Lab(
        Network Network, EthernetTransmissionService Ethernet, IPv4Layer Ipv4, ArpLayer Arp,
        TcpLayer Tcp, ITcpConnectionManager Connections, NetworkInterface Pc0, NetworkInterface Pc1);

    private static Lab BuildLab()
    {
        var network = new Network("Lab");
        var ethernet = new EthernetTransmissionService(new PacketEngine());
        var ipv4 = new IPv4Layer(new IPv4Processor());
        var arp = new ArpLayer(new ArpProcessor());
        var connections = new TcpConnectionManager(new SequentialInitialSequenceNumberGenerator());
        var tcp = new TcpLayer(new TcpProcessor(), connections);

        var pc0 = NetworkDeviceFactory.Create(DeviceType.Pc, "PC0");
        var pc1 = NetworkDeviceFactory.Create(DeviceType.Pc, "PC1");
        network.AddDevice(pc0);
        network.AddDevice(pc1);

        var g0 = pc0.Interfaces.Single();
        var g1 = pc1.Interfaces.Single();
        g0.AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(Pc0Ip, 24));
        g1.AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(Pc1Ip, 24));
        network.Connect(g0, g1);
        g0.BringUp();
        g1.BringUp();

        return new Lab(network, ethernet, ipv4, arp, tcp, connections, g0, g1);
    }

    private static EthernetFrame DeliveredFrame(EthernetTransmissionResult result) =>
        Assert.IsType<EthernetFrame>(result.Packet!.Payload);

    private static MacAddress ResolveMac(Lab lab, NetworkInterface from, NetworkInterface to, IPv4Address toIp)
    {
        var resolution = lab.Arp.Resolve(from, toIp);
        if (resolution.IsResolved)
        {
            return resolution.HardwareAddress!.Value;
        }

        var toDest = lab.Ethernet.Transmit(lab.Network, from, resolution.RequestFrame!);
        var destReport = lab.Arp.HandleIncoming(to, DeliveredFrame(toDest));
        var toSource = lab.Ethernet.Transmit(lab.Network, to, destReport.ReplyFrame!);
        lab.Arp.HandleIncoming(from, DeliveredFrame(toSource));

        Assert.True(from.ArpCache.TryResolve(toIp, out var mac));
        return mac;
    }

    /// <summary>Sends one TCP segment from <paramref name="fromInterface"/> to <paramref name="toInterface"/> and returns the delivered <see cref="TcpProcessingReport"/>.</summary>
    private static TcpProcessingReport SendAndDeliver(
        Lab lab, NetworkInterface fromInterface, NetworkInterface toInterface, IPv4Address fromIp, IPv4Address toIp, MacAddress toMac, TcpSegment segment)
    {
        var ip = lab.Tcp.Encapsulate(segment, fromIp, toIp);
        var frame = lab.Ipv4.Encapsulate(ip, fromInterface.MacAddress!.Value, toMac);
        var transmission = lab.Ethernet.Transmit(lab.Network, fromInterface, frame);
        Assert.True(transmission.IsSuccess);

        var deliveredIp = Assert.IsType<IPv4Packet>(DeliveredFrame(transmission).Payload);
        return lab.Tcp.HandleIncoming(toInterface, deliveredIp);
    }

    [Fact]
    public void FullConnection_Pc0ToPc1Port80_HandshakeDataAndGracefulClose()
    {
        var lab = BuildLab();
        lab.Connections.Listen(lab.Pc1.Device, ServerPort);
        var pc1Mac = ResolveMac(lab, lab.Pc0, lab.Pc1, Pc1Ip);
        var pc0Mac = ResolveMac(lab, lab.Pc1, lab.Pc0, Pc0Ip);

        // 1. SYN: PC0 actively opens toward PC1:80.
        var connectResult = lab.Connections.Connect(lab.Pc0, Pc1Ip, ServerPort, ClientPort);
        var client = connectResult.Connection;
        Assert.Equal(TcpConnectionState.SynSent, client.State);

        // 2. SYN+ACK: PC1 accepts on its listener.
        var synReport = SendAndDeliver(lab, lab.Pc0, lab.Pc1, Pc0Ip, Pc1Ip, pc1Mac, connectResult.Syn);
        Assert.Equal(TcpProcessingOutcomeKind.SynReceived, synReport.Kind);
        var server = synReport.Connection!;
        Assert.Equal(TcpConnectionState.SynReceived, server.State);

        // 3. ACK: PC0 completes the handshake.
        var handshakeReport = SendAndDeliver(lab, lab.Pc1, lab.Pc0, Pc1Ip, Pc0Ip, pc0Mac, synReport.ResponseSegment!);
        Assert.Equal(TcpProcessingOutcomeKind.HandshakeCompleted, handshakeReport.Kind);
        Assert.Equal(TcpConnectionState.Established, client.State);

        var establishedReport = SendAndDeliver(lab, lab.Pc0, lab.Pc1, Pc0Ip, Pc1Ip, pc1Mac, handshakeReport.ResponseSegment!);
        Assert.Equal(TcpProcessingOutcomeKind.ConnectionEstablished, establishedReport.Kind);
        Assert.Equal(TcpConnectionState.Established, server.State);

        // 4. Data: PC0 sends "Hello", PC1 delivers it and ACKs.
        var dataSegment = lab.Connections.Send(client, RawPayload.FromText("Hello"));
        var dataReport = SendAndDeliver(lab, lab.Pc0, lab.Pc1, Pc0Ip, Pc1Ip, pc1Mac, dataSegment);
        Assert.Equal(TcpProcessingOutcomeKind.DataDelivered, dataReport.Kind);
        Assert.Equal("Hello", Encoding.UTF8.GetString(((RawPayload)dataReport.DeliveredPayload!).Data.Span));

        var dataAckReport = SendAndDeliver(lab, lab.Pc1, lab.Pc0, Pc1Ip, Pc0Ip, pc0Mac, dataReport.ResponseSegment!);
        Assert.Equal(TcpProcessingOutcomeKind.AckProcessed, dataAckReport.Kind);

        // 5. FIN/ACK: PC0 closes; PC1 acknowledges and closes its own side; PC0 acknowledges PC1's FIN.
        var clientFin = lab.Connections.Close(client);
        var finReport = SendAndDeliver(lab, lab.Pc0, lab.Pc1, Pc0Ip, Pc1Ip, pc1Mac, clientFin);
        Assert.Equal(TcpProcessingOutcomeKind.FinReceived, finReport.Kind);
        Assert.Equal(TcpConnectionState.CloseWait, server.State);

        var ackOfClientFinReport = SendAndDeliver(lab, lab.Pc1, lab.Pc0, Pc1Ip, Pc0Ip, pc0Mac, finReport.ResponseSegment!);
        Assert.Equal(TcpConnectionState.FinWait2, client.State);

        var serverFin = lab.Connections.Close(server);
        var serverFinReport = SendAndDeliver(lab, lab.Pc1, lab.Pc0, Pc1Ip, Pc0Ip, pc0Mac, serverFin);
        Assert.Equal(TcpProcessingOutcomeKind.FinReceived, serverFinReport.Kind);
        Assert.Equal(TcpConnectionState.TimeWait, client.State);

        var finalReport = SendAndDeliver(lab, lab.Pc0, lab.Pc1, Pc0Ip, Pc1Ip, pc1Mac, serverFinReport.ResponseSegment!);
        Assert.Equal(TcpProcessingOutcomeKind.ConnectionClosed, finalReport.Kind);
        Assert.Equal(TcpConnectionState.Closed, server.State);
    }

    [Fact]
    public void ConnectingToANonListeningPort_IsRefusedWithReset()
    {
        var lab = BuildLab();
        var pc1Mac = ResolveMac(lab, lab.Pc0, lab.Pc1, Pc1Ip);

        var connectResult = lab.Connections.Connect(lab.Pc0, Pc1Ip, ServerPort, ClientPort);
        var report = SendAndDeliver(lab, lab.Pc0, lab.Pc1, Pc0Ip, Pc1Ip, pc1Mac, connectResult.Syn);

        Assert.Equal(TcpProcessingOutcomeKind.ConnectionRefused, report.Kind);
        Assert.True(report.ResponseSegment!.IsRst);
    }

    [Fact]
    public void CorruptedSegment_IsDroppedByTheProcessor_WithoutCrashing()
    {
        var lab = BuildLab();
        lab.Connections.Listen(lab.Pc1.Device, ServerPort);
        var pc1Mac = ResolveMac(lab, lab.Pc0, lab.Pc1, Pc1Ip);
        var connectResult = lab.Connections.Connect(lab.Pc0, Pc1Ip, ServerPort, ClientPort);
        var corrupted = connectResult.Syn.WithChecksum(unchecked((ushort)(connectResult.Syn.Checksum + 1)));

        var report = SendAndDeliver(lab, lab.Pc0, lab.Pc1, Pc0Ip, Pc1Ip, pc1Mac, corrupted);

        Assert.False(report.IsSuccess);
        Assert.Same(TcpDropReasons.InvalidChecksum, report.DropReason);
    }
}
