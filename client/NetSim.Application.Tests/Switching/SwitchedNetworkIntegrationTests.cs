using System.Collections.Generic;
using System.Linq;
using NetSim.Application.Dhcp;
using NetSim.Application.Diagnostics;
using NetSim.Application.Dns;
using NetSim.Application.Services;
using NetSim.Application.State;
using NetSim.Application.Tests.TestSupport;
using NetSim.Core.Arp;
using NetSim.Core.Devices;
using NetSim.Core.Dhcp;
using NetSim.Core.Dns;
using NetSim.Core.Ethernet;
using NetSim.Core.Icmp;
using NetSim.Core.IP;
using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Switching;
using NetSim.Core.Topology;
using NetSim.Core.Udp;

namespace NetSim.Application.Tests.Switching;

/// <summary>
/// Phase 25 end-to-end: the existing ARP / ICMP-Ping / DHCP orchestrators must keep working when a
/// Layer 2 switch (or a chain of switches) sits between the endpoints - the switch learns MACs,
/// floods broadcasts and unknown unicasts, and forwards known unicasts directly.
/// </summary>
public class SwitchedNetworkIntegrationTests
{
    private static NetworkInterface Nic(NetworkDevice d) => d.Interfaces.Single();

    private static NetworkInterface Port(NetworkDevice sw, int n) => sw.Interfaces.First(p => p.Name == $"FastEthernet0/{n}");

    private static void ConnectUp(Network net, NetworkInterface a, NetworkInterface b)
    {
        net.Connect(a, b);
        a.BringUp();
        b.BringUp();
    }

    // ---- Ping through a switch (brief scenario J) -----------------------------------------

    [Fact]
    public void Ping_PcSwitchPc_Succeeds_AndTheSwitchLearnsBothHosts_ThenForwardsWithoutFlooding()
    {
        var networkService = new NetworkService(new ApplicationState());
        var network = networkService.CreateNetwork("Lab");

        var pc1 = NetworkDeviceFactory.Create(DeviceType.Pc, "PC1");
        var pc2 = NetworkDeviceFactory.Create(DeviceType.Pc, "PC2");
        var sw = NetworkDeviceFactory.Create(DeviceType.Switch, "SW1");
        network.AddDevice(pc1);
        network.AddDevice(pc2);
        network.AddDevice(sw);

        Nic(pc1).AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("192.168.1.10"), 24));
        Nic(pc2).AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("192.168.1.20"), 24));
        ConnectUp(network, Nic(pc1), Port(sw, 1));
        ConnectUp(network, Nic(pc2), Port(sw, 2));

        var ethernet = new EthernetTransmissionService(new PacketEngine());
        var engine = new SwitchingEngine(new ManualSimulationClock());
        var floods = 0;
        var directForwards = 0;
        engine.UnknownUnicastFlooded += (_, _) => floods++;
        engine.FrameForwarded += (_, _) => directForwards++;
        var segment = new SwitchedSegmentService(ethernet, engine);

        var service = new PingService(
            new IcmpLayer(new IcmpProcessor()), new IPv4Layer(new IPv4Processor()),
            new ArpLayer(new ArpProcessor()), ethernet, networkService, segment);

        var first = service.Ping(Nic(pc1), IPv4Address.Parse("192.168.1.20"), count: 1);
        Assert.True(first.IsFullSuccess);

        Assert.True(((Core.Devices.Switch)sw).MacAddressTable.Contains(Nic(pc1).MacAddress!.Value));
        Assert.True(((Core.Devices.Switch)sw).MacAddressTable.Contains(Nic(pc2).MacAddress!.Value));

        // Second ping: both MACs are known now, so the switch forwards directly and never floods.
        directForwards = 0;
        floods = 0;
        var second = service.Ping(Nic(pc1), IPv4Address.Parse("192.168.1.20"), count: 2);
        Assert.True(second.IsFullSuccess);
        Assert.Equal(0, floods);
        Assert.True(directForwards > 0);
    }

    [Fact]
    public void Ping_AcrossTwoSwitches_Succeeds()
    {
        var networkService = new NetworkService(new ApplicationState());
        var network = networkService.CreateNetwork("Lab");

        var pc1 = NetworkDeviceFactory.Create(DeviceType.Pc, "PC1");
        var pc2 = NetworkDeviceFactory.Create(DeviceType.Pc, "PC2");
        var sw1 = NetworkDeviceFactory.Create(DeviceType.Switch, "SW1");
        var sw2 = NetworkDeviceFactory.Create(DeviceType.Switch, "SW2");
        foreach (var d in new[] { pc1, pc2, sw1, sw2 })
        {
            network.AddDevice(d);
        }

        Nic(pc1).AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("10.0.0.1"), 24));
        Nic(pc2).AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("10.0.0.2"), 24));
        ConnectUp(network, Nic(pc1), Port(sw1, 1));
        ConnectUp(network, Port(sw1, 2), Port(sw2, 1));
        ConnectUp(network, Nic(pc2), Port(sw2, 2));

        var ethernet = new EthernetTransmissionService(new PacketEngine());
        var service = new PingService(
            new IcmpLayer(new IcmpProcessor()), new IPv4Layer(new IPv4Processor()),
            new ArpLayer(new ArpProcessor()), ethernet, networkService,
            new SwitchedSegmentService(ethernet, new SwitchingEngine(new ManualSimulationClock())));

        var result = service.Ping(Nic(pc1), IPv4Address.Parse("10.0.0.2"), count: 2);

        Assert.True(result.IsFullSuccess);
    }

    // ---- ARP through a switch (brief scenario I) -----------------------------------------

    [Fact]
    public void Arp_RequestIsFlooded_ReplyIsForwardedAsKnownUnicast()
    {
        var network = new Network("Lab");
        var pc1 = NetworkDeviceFactory.Create(DeviceType.Pc, "PC1");
        var pc2 = NetworkDeviceFactory.Create(DeviceType.Pc, "PC2");
        var sw = NetworkDeviceFactory.Create(DeviceType.Switch, "SW1");
        foreach (var d in new[] { pc1, pc2, sw })
        {
            network.AddDevice(d);
        }

        var pc1Ip = IPv4Address.Parse("192.168.0.1");
        var pc2Ip = IPv4Address.Parse("192.168.0.2");
        Nic(pc1).AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(pc1Ip, 24));
        Nic(pc2).AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(pc2Ip, 24));
        ConnectUp(network, Nic(pc1), Port(sw, 1));
        ConnectUp(network, Nic(pc2), Port(sw, 2));

        var ethernet = new EthernetTransmissionService(new PacketEngine());
        var engine = new SwitchingEngine(new ManualSimulationClock());
        var decisions = new List<SwitchForwardingDecision>();
        engine.FrameReceived += (_, _) => { };
        engine.BroadcastFlooded += (_, _) => decisions.Add(SwitchForwardingDecision.BroadcastFlood);
        engine.FrameForwarded += (_, _) => decisions.Add(SwitchForwardingDecision.KnownUnicast);
        var segment = new SwitchedSegmentService(ethernet, engine);
        var arp = new ArpLayer(new ArpProcessor());

        // 1. PC1 resolves PC2 -> cache miss -> broadcast ARP request across the segment.
        var resolution = arp.Resolve(Nic(pc1), pc2Ip);
        Assert.False(resolution.IsResolved);
        var requestOut = segment.TransmitAcrossSegment(network, Nic(pc1), resolution.RequestFrame!);
        var atPc2 = requestOut.DeliveryTo(Nic(pc2));
        Assert.NotNull(atPc2);

        // 2. PC2 answers with a unicast reply; the switch has learned PC1, so it forwards directly.
        var report = arp.HandleIncoming(Nic(pc2), atPc2!.Frame);
        Assert.True(report.ReplyGenerated);
        var replyOut = segment.TransmitAcrossSegment(network, Nic(pc2), report.ReplyFrame!);
        var atPc1 = replyOut.DeliveryTo(Nic(pc1));
        Assert.NotNull(atPc1);
        arp.HandleIncoming(Nic(pc1), atPc1!.Frame);

        Assert.True(Nic(pc1).ArpCache.TryResolve(pc2Ip, out var learned));
        Assert.Equal(Nic(pc2).MacAddress!.Value, learned);
        Assert.Equal(SwitchForwardingDecision.BroadcastFlood, decisions[0]);
        Assert.Equal(SwitchForwardingDecision.KnownUnicast, decisions[1]);
    }

    // ---- DHCP through a switch (brief section 15, scenario H, section 38) ----------------

    [Fact]
    public void Dhcp_DoraExchange_WorksThroughASwitch_AndTheSwitchLearnsClientAndServer()
    {
        var networkService = new NetworkService(new ApplicationState());
        var network = networkService.CreateNetwork("Lab");

        var pc0 = NetworkDeviceFactory.Create(DeviceType.Pc, "PC0");
        var server0 = NetworkDeviceFactory.Create(DeviceType.Server, "Server0");
        var sw = NetworkDeviceFactory.Create(DeviceType.Switch, "SW1");
        foreach (var d in new[] { pc0, server0, sw })
        {
            network.AddDevice(d);
        }

        var serverIp = IPv4Address.Parse("192.168.1.1");
        Nic(server0).AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(serverIp, 24));
        ConnectUp(network, Nic(pc0), Port(sw, 1));
        ConnectUp(network, Nic(server0), Port(sw, 2));

        var ethernet = new EthernetTransmissionService(new PacketEngine());
        var udpDelivery = new UdpDeliveryManager();
        var udp = new UdpLayer(new UdpProcessor(), udpDelivery);
        var ipv4 = new IPv4Layer(new IPv4Processor());
        var time = TestTimeProvider.StartingAtEpoch();
        var serverService = new DhcpServerService(udpDelivery);
        var engine = new SwitchingEngine(new ManualSimulationClock());
        var segment = new SwitchedSegmentService(ethernet, engine);

        var config = new DhcpServerConfiguration(
            serverIp, 24, IPv4Address.Parse("192.168.1.100"), IPv4Address.Parse("192.168.1.150"),
            defaultGateway: serverIp, dnsServer: IPv4Address.Parse("192.168.1.53"), leaseDuration: TimeSpan.FromSeconds(3600));
        serverService.Start(server0, new DhcpServer(config, time));

        var client = new DhcpClient(
            udp, ipv4, ethernet, networkService, serverService, new DnsClientConfigurationStore(),
            new DhcpClientStateStore(), new SequentialDhcpTransactionIdSource(), time, segment);

        var events = new List<string>();
        client.DiscoverSent += (_, _) => events.Add("discover");
        client.OfferReceived += (_, _) => events.Add("offer");
        client.RequestSent += (_, _) => events.Add("request");
        client.AckReceived += (_, _) => events.Add("ack");
        client.LeaseBound += (_, _) => events.Add("bound");

        var result = client.Acquire(Nic(pc0));

        Assert.True(result.IsSuccess);
        Assert.Equal(DhcpAcquireStatus.Bound, result.Status);
        Assert.Equal(IPv4Address.Parse("192.168.1.100"), result.Address);
        Assert.Equal("192.168.1.100/24", Nic(pc0).PrimaryIPv4Configuration!.Cidr);
        Assert.Equal(new[] { "discover", "offer", "request", "ack", "bound" }, events);

        var macTable = ((Core.Devices.Switch)sw).MacAddressTable;
        Assert.True(macTable.Contains(Nic(pc0).MacAddress!.Value));
        Assert.True(macTable.Contains(Nic(server0).MacAddress!.Value));
    }

    [Fact]
    public void Dhcp_ThenPing_WorkTogetherThroughTheSameSwitch()
    {
        var networkService = new NetworkService(new ApplicationState());
        var network = networkService.CreateNetwork("Lab");

        var pc0 = NetworkDeviceFactory.Create(DeviceType.Pc, "PC0");
        var server0 = NetworkDeviceFactory.Create(DeviceType.Server, "Server0");
        var sw = NetworkDeviceFactory.Create(DeviceType.Switch, "SW1");
        foreach (var d in new[] { pc0, server0, sw })
        {
            network.AddDevice(d);
        }

        var serverIp = IPv4Address.Parse("192.168.1.1");
        Nic(server0).AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(serverIp, 24));
        ConnectUp(network, Nic(pc0), Port(sw, 1));
        ConnectUp(network, Nic(server0), Port(sw, 2));

        var ethernet = new EthernetTransmissionService(new PacketEngine());
        var udpDelivery = new UdpDeliveryManager();
        var udp = new UdpLayer(new UdpProcessor(), udpDelivery);
        var ipv4 = new IPv4Layer(new IPv4Processor());
        var time = TestTimeProvider.StartingAtEpoch();
        var serverService = new DhcpServerService(udpDelivery);
        var segment = new SwitchedSegmentService(ethernet, new SwitchingEngine(new ManualSimulationClock()));

        var config = new DhcpServerConfiguration(
            serverIp, 24, IPv4Address.Parse("192.168.1.100"), IPv4Address.Parse("192.168.1.150"),
            defaultGateway: serverIp, dnsServer: serverIp, leaseDuration: TimeSpan.FromSeconds(3600));
        serverService.Start(server0, new DhcpServer(config, time));

        var client = new DhcpClient(
            udp, ipv4, ethernet, networkService, serverService, new DnsClientConfigurationStore(),
            new DhcpClientStateStore(), new SequentialDhcpTransactionIdSource(), time, segment);
        Assert.True(client.Acquire(Nic(pc0)).IsSuccess);

        var ping = new PingService(
            new IcmpLayer(new IcmpProcessor()), ipv4, new ArpLayer(new ArpProcessor()), ethernet, networkService, segment);

        var result = ping.Ping(Nic(pc0), serverIp, count: 2);

        Assert.True(result.IsFullSuccess);
    }
}
