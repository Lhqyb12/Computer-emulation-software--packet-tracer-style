using System.Linq;
using NetSim.Core.Arp;
using NetSim.Core.Devices;
using NetSim.Core.Dns;
using NetSim.Core.Ethernet;
using NetSim.Core.IP;
using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Tcp;
using NetSim.Core.Topology;
using NetSim.Core.Transport;

namespace NetSim.Core.Tests.Dns;

/// <summary>
/// Proves the DNS-over-TCP architecture the brief asks for (section 5: "design the architecture so
/// that TCP can be used where appropriate... without implementing a complete production-grade TCP
/// DNS stack"): <see cref="DnsMessage"/> rides as a <see cref="TcpSegment"/> payload exactly like it
/// rides as a <see cref="Udp.UdpDatagram"/> payload, and <see cref="DnsServer.HandleQuery"/> is fully
/// transport-agnostic - the same method answers both. This test drives the full three-way handshake
/// and a query/response exchange manually (mirroring <c>TcpHandshakeIntegrationTests</c>), rather
/// than through a polished resolver API, matching the brief's "architecture, not a full stack" scope.
/// </summary>
public class DnsTcpIntegrationTests
{
    private static readonly IPv4Address ClientIp = IPv4Address.Parse("192.168.1.10");
    private static readonly IPv4Address ServerIp = IPv4Address.Parse("192.168.1.53");
    private static readonly DomainName Example = DomainName.Parse("example.com");

    private sealed record Lab(
        Network Network, EthernetTransmissionService Ethernet, IPv4Layer Ipv4, ArpLayer Arp,
        TcpLayer Tcp, TcpConnectionManager Connections, NetworkInterface Client, NetworkInterface Server, DnsServer DnsServer);

    private static Lab BuildLab()
    {
        var network = new Network("Lab");
        var ethernet = new EthernetTransmissionService(new PacketEngine());
        var ipv4 = new IPv4Layer(new IPv4Processor());
        var arp = new ArpLayer(new ArpProcessor());
        var connections = new TcpConnectionManager(new SequentialInitialSequenceNumberGenerator());
        var tcp = new TcpLayer(new TcpProcessor(), connections);

        var client = NetworkDeviceFactory.Create(DeviceType.Pc, "PC0");
        var server = NetworkDeviceFactory.Create(DeviceType.Server, "Server0");
        network.AddDevice(client);
        network.AddDevice(server);

        var clientInterface = client.Interfaces.Single();
        var serverInterface = server.Interfaces.Single();
        clientInterface.AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(ClientIp, 24));
        serverInterface.AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(ServerIp, 24));
        network.Connect(clientInterface, serverInterface);
        clientInterface.BringUp();
        serverInterface.BringUp();

        var dnsServer = new DnsServer(Example);
        dnsServer.AddRecord(new DnsARecord(Example, IPv4Address.Parse("192.168.1.100"), TimeSpan.FromSeconds(300)));

        return new Lab(network, ethernet, ipv4, arp, tcp, connections, clientInterface, serverInterface, dnsServer);
    }

    private static EthernetFrame DeliveredFrame(EthernetTransmissionResult result) => Assert.IsType<EthernetFrame>(result.Packet!.Payload);

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
    public void DnsQueryAndResponse_TravelOverAnEstablishedTcpConnection()
    {
        var lab = BuildLab();
        DnsMessage? pendingResponse = null;

        // The listener's onDataReceived callback is exactly the seam UdpBinding also offers -
        // DnsServer.HandleQuery itself needs no network context at all.
        lab.Connections.Listen(lab.Server.Device, DnsProtocol.Port, onDataReceived: (connection, payload) =>
        {
            if (payload is DnsMessage query)
            {
                pendingResponse = lab.DnsServer.HandleQuery(query, ClientIp);
            }
        });

        var clientMac = ResolveMac(lab, lab.Client, lab.Server, ServerIp);
        var serverMac = ResolveMac(lab, lab.Server, lab.Client, ClientIp);

        // Three-way handshake.
        var connectResult = lab.Connections.Connect(lab.Client, ServerIp, DnsProtocol.Port, Port.Create(50000));
        var clientConnection = connectResult.Connection;
        var synReport = SendAndDeliver(lab, lab.Client, lab.Server, ClientIp, ServerIp, clientMac, connectResult.Syn);
        var serverConnection = synReport.Connection!;
        var handshakeReport = SendAndDeliver(lab, lab.Server, lab.Client, ServerIp, ClientIp, serverMac, synReport.ResponseSegment!);
        var establishedReport = SendAndDeliver(lab, lab.Client, lab.Server, ClientIp, ServerIp, serverMac, handshakeReport.ResponseSegment!);
        Assert.Equal(TcpProcessingOutcomeKind.ConnectionEstablished, establishedReport.Kind);
        Assert.Equal(TcpConnectionState.Established, clientConnection.State);
        Assert.Equal(TcpConnectionState.Established, serverConnection.State);

        // Client sends the DNS query as TCP data.
        var query = DnsMessage.CreateQuery(4242, Example, DnsRecordType.A);
        var querySegment = lab.Connections.Send(clientConnection, query);
        var queryReport = SendAndDeliver(lab, lab.Client, lab.Server, ClientIp, ServerIp, serverMac, querySegment);
        Assert.Equal(TcpProcessingOutcomeKind.DataDelivered, queryReport.Kind);
        Assert.NotNull(pendingResponse);
        Assert.Equal(4242, pendingResponse!.Header.TransactionId);
        Assert.Equal(DnsResponseCode.NoError, pendingResponse.Header.ResponseCode);

        // Server acknowledges the query segment.
        SendAndDeliver(lab, lab.Server, lab.Client, ServerIp, ClientIp, clientMac, queryReport.ResponseSegment!);

        // Server sends the DNS response back as TCP data on the same connection.
        var responseSegment = lab.Connections.Send(serverConnection, pendingResponse);
        var responseReport = SendAndDeliver(lab, lab.Server, lab.Client, ServerIp, ClientIp, clientMac, responseSegment);

        Assert.Equal(TcpProcessingOutcomeKind.DataDelivered, responseReport.Kind);
        var receivedResponse = Assert.IsType<DnsMessage>(responseReport.DeliveredPayload);
        Assert.Equal(4242, receivedResponse.Header.TransactionId);
        var answer = Assert.IsType<DnsARecord>(Assert.Single(receivedResponse.Answers));
        Assert.Equal(IPv4Address.Parse("192.168.1.100"), answer.Address);
    }
}
