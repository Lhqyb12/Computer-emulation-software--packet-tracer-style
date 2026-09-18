using System.Linq;
using NetSim.Application.Diagnostics;
using NetSim.Application.Services;
using NetSim.Application.State;
using NetSim.Core.Arp;
using NetSim.Core.Devices;
using NetSim.Core.Ethernet;
using NetSim.Core.Icmp;
using NetSim.Core.IP;
using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Routing;
using NetSim.Core.Switching;
using NetSim.Core.Topology;

namespace NetSim.Application.Tests.Routing;

/// <summary>
/// Phase 27 sections 47-50, 71: the primary acceptance scenario end to end -
/// <c>PC1 -&gt; SW1 -&gt; R1 -&gt; SW2 -&gt; PC2</c> - driven through the real <see cref="PingService"/>
/// over the real Core engines (ARP / ICMP / IPv4 / Ethernet / switching / routing). No fake result.
/// </summary>
public class RoutedPingIntegrationTests
{
    private static readonly IPv4Address Pc1Ip = IPv4Address.Parse("192.168.1.10");
    private static readonly IPv4Address Pc2Ip = IPv4Address.Parse("192.168.2.10");
    private static readonly IPv4Address R1Near = IPv4Address.Parse("192.168.1.1");
    private static readonly IPv4Address R1Far = IPv4Address.Parse("192.168.2.1");

    private sealed record Lab(PingService Service, Network Network, Router R1, NetworkInterface Pc1, NetworkInterface Pc2);

    private static void ConnectUp(Network net, NetworkInterface a, NetworkInterface b)
    {
        net.Connect(a, b);
        a.BringUp();
        b.BringUp();
    }

    private static NetworkInterface Port(NetworkDevice sw, int n) => sw.Interfaces.First(p => p.Name == $"FastEthernet0/{n}");

    // PC1(192.168.1.10) - SW1 - R1[Gi0/0 192.168.1.1 | Gi0/1 192.168.2.1] - SW2 - PC2(192.168.2.10)
    private static Lab BuildLab(bool configureGateways = true)
    {
        var networkService = new NetworkService(new ApplicationState());
        var network = networkService.CreateNetwork("Lab");

        var pc1 = NetworkDeviceFactory.Create(DeviceType.Pc, "PC1");
        var pc2 = NetworkDeviceFactory.Create(DeviceType.Pc, "PC2");
        var sw1 = NetworkDeviceFactory.Create(DeviceType.Switch, "SW1");
        var sw2 = NetworkDeviceFactory.Create(DeviceType.Switch, "SW2");
        var r1 = (Router)NetworkDeviceFactory.Create(DeviceType.Router, "R1");
        foreach (var d in new[] { pc1, pc2, sw1, sw2, r1 })
        {
            network.AddDevice(d);
        }

        var pc1Nic = pc1.Interfaces.Single();
        var pc2Nic = pc2.Interfaces.Single();
        var gi0 = r1.Interfaces.First(i => i.Name == "GigabitEthernet0/0");
        var gi1 = r1.Interfaces.First(i => i.Name == "GigabitEthernet0/1");

        pc1Nic.SetPrimaryIPv4Configuration(Ipv4InterfaceConfiguration.Create(Pc1Ip, 24));
        pc2Nic.SetPrimaryIPv4Configuration(Ipv4InterfaceConfiguration.Create(Pc2Ip, 24));
        gi0.SetPrimaryIPv4Configuration(Ipv4InterfaceConfiguration.Create(R1Near, 24));
        gi1.SetPrimaryIPv4Configuration(Ipv4InterfaceConfiguration.Create(R1Far, 24));
        r1.SyncConnectedRoutes();

        if (configureGateways)
        {
            pc1Nic.SetIPv4DefaultGateway(R1Near);
            pc2Nic.SetIPv4DefaultGateway(R1Far);
        }

        ConnectUp(network, pc1Nic, Port(sw1, 1));
        ConnectUp(network, Port(sw1, 2), gi0);
        ConnectUp(network, gi1, Port(sw2, 1));
        ConnectUp(network, Port(sw2, 2), pc2Nic);

        var ethernet = new EthernetTransmissionService(new PacketEngine());
        var segment = new SwitchedSegmentService(ethernet, new SwitchingEngine());
        var service = new PingService(
            new IcmpLayer(new IcmpProcessor()), new IPv4Layer(new IPv4Processor()),
            new ArpLayer(new ArpProcessor()), ethernet, networkService, segment);

        return new Lab(service, network, r1, pc1Nic, pc2Nic);
    }

    [Fact]
    public void Ping_Pc1_To_Pc2_AcrossTheRouter_Succeeds()
    {
        var lab = BuildLab();

        var result = lab.Service.Ping(lab.Pc1, Pc2Ip, count: 3);

        Assert.Equal(3, result.PacketsSent);
        Assert.Equal(3, result.PacketsReceived);
        Assert.True(result.IsFullSuccess);
        Assert.All(result.Replies, r => Assert.Equal(Pc2Ip, r.RespondingAddress));
    }

    [Fact]
    public void Ping_AcrossTheRouter_DecrementsTtlByExactlyOneHop()
    {
        var lab = BuildLab();

        var reply = lab.Service.Ping(lab.Pc1, Pc2Ip, count: 1).Replies.Single();

        Assert.True(reply.IsSuccess);
        // PC2 sends the Echo Reply at TTL 64; it crosses R1 once on the way back.
        Assert.Equal((byte)(IPv4Packet.DefaultTimeToLive - 1), reply.TimeToLive);
    }

    [Fact]
    public void Ping_Pc1_To_ItsDefaultGatewayInterface_IsLocalDelivery_AndSucceeds()
    {
        var lab = BuildLab();

        var result = lab.Service.Ping(lab.Pc1, R1Near, count: 2);

        Assert.True(result.IsFullSuccess);
        Assert.All(result.Replies, r => Assert.Equal(R1Near, r.RespondingAddress));
        // No routing hop: the reply comes straight back at the initial TTL.
        Assert.All(result.Replies, r => Assert.Equal(IPv4Packet.DefaultTimeToLive, r.TimeToLive));
    }

    [Fact]
    public void Ping_Pc1_To_TheRoutersFarInterface_ReachesItThroughTheRouter()
    {
        var lab = BuildLab();

        var result = lab.Service.Ping(lab.Pc1, R1Far, count: 1);

        Assert.True(result.IsFullSuccess);
        Assert.Equal(R1Far, result.Replies.Single().RespondingAddress);
    }

    [Fact]
    public void Ping_OffSubnet_WithNoDefaultGateway_IsReportedAsNoRoute()
    {
        var lab = BuildLab(configureGateways: false);

        var reply = lab.Service.Ping(lab.Pc1, Pc2Ip, count: 1).Replies.Single();

        Assert.False(reply.IsSuccess);
        Assert.Equal(PingReplyStatus.NoRoute, reply.Status);
    }

    [Fact]
    public void Ping_ThroughARouterWhoseFarInterfaceIsDown_Fails_WithNoRoute()
    {
        var lab = BuildLab();
        lab.R1.Interfaces.First(i => i.Name == "GigabitEthernet0/1").BringDown();

        var reply = lab.Service.Ping(lab.Pc1, Pc2Ip, count: 1).Replies.Single();

        Assert.False(reply.IsSuccess);
        Assert.Equal(PingReplyStatus.NoRoute, reply.Status);
    }

    [Fact]
    public void Router_KnowsExactlyItsTwoDirectlyConnectedNetworks()
    {
        var lab = BuildLab();

        var routes = lab.R1.RoutingTable.GetRoutes();

        Assert.Equal(2, routes.Count);
        Assert.All(routes, r => Assert.Equal(RouteType.Connected, r.Type));
        Assert.Contains(routes, r => r.Destination.Equals(IPv4Network.Parse("192.168.1.0/24")));
        Assert.Contains(routes, r => r.Destination.Equals(IPv4Network.Parse("192.168.2.0/24")));
    }

    // ---- Section 48: two routers, connected routes only (no static routes) -------------------

    [Fact]
    public void TwoRouters_ConnectedRoutesOnly_EachKnowsOnlyItsOwnNetworks_AndEndToEndDoesNotYetWork()
    {
        var networkService = new NetworkService(new ApplicationState());
        var network = networkService.CreateNetwork("Lab");

        var pc1 = NetworkDeviceFactory.Create(DeviceType.Pc, "PC1");
        var pc2 = NetworkDeviceFactory.Create(DeviceType.Pc, "PC2");
        var r1 = (Router)NetworkDeviceFactory.Create(DeviceType.Router, "R1");
        var r2 = (Router)NetworkDeviceFactory.Create(DeviceType.Router, "R2");
        foreach (var d in new[] { pc1, pc2, r1, r2 })
        {
            network.AddDevice(d);
        }

        var pc1Nic = pc1.Interfaces.Single();
        var pc2Nic = pc2.Interfaces.Single();
        var r1Lan = r1.Interfaces.First(i => i.Name == "GigabitEthernet0/0");
        var r1Link = r1.Interfaces.First(i => i.Name == "GigabitEthernet0/1");
        var r2Link = r2.Interfaces.First(i => i.Name == "GigabitEthernet0/0");
        var r2Lan = r2.Interfaces.First(i => i.Name == "GigabitEthernet0/1");

        pc1Nic.SetPrimaryIPv4Configuration(Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("192.168.1.10"), 24));
        pc2Nic.SetPrimaryIPv4Configuration(Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("192.168.2.10"), 24));
        r1Lan.SetPrimaryIPv4Configuration(Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("192.168.1.1"), 24));
        r1Link.SetPrimaryIPv4Configuration(Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("10.0.0.1"), 30));
        r2Link.SetPrimaryIPv4Configuration(Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("10.0.0.2"), 30));
        r2Lan.SetPrimaryIPv4Configuration(Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("192.168.2.1"), 24));
        r1.SyncConnectedRoutes();
        r2.SyncConnectedRoutes();

        pc1Nic.SetIPv4DefaultGateway(IPv4Address.Parse("192.168.1.1"));
        pc2Nic.SetIPv4DefaultGateway(IPv4Address.Parse("192.168.2.1"));

        ConnectUp(network, pc1Nic, r1Lan);
        ConnectUp(network, r1Link, r2Link);
        ConnectUp(network, r2Lan, pc2Nic);

        var ethernet = new EthernetTransmissionService(new PacketEngine());
        var segment = new SwitchedSegmentService(ethernet, new SwitchingEngine());
        var service = new PingService(
            new IcmpLayer(new IcmpProcessor()), new IPv4Layer(new IPv4Processor()),
            new ArpLayer(new ArpProcessor()), ethernet, networkService, segment);

        // Each router knows only its two directly connected networks - no route to the far LAN.
        Assert.Equal(
            new[] { IPv4Network.Parse("10.0.0.0/30"), IPv4Network.Parse("192.168.1.0/24") },
            r1.RoutingTable.GetRoutes().Select(r => r.Destination).OrderBy(n => n.NetworkAddress).ToArray());
        Assert.Equal(
            new[] { IPv4Network.Parse("10.0.0.0/30"), IPv4Network.Parse("192.168.2.0/24") },
            r2.RoutingTable.GetRoutes().Select(r => r.Destination).OrderBy(n => n.NetworkAddress).ToArray());

        // Without static/dynamic routes, PC1 cannot reach PC2 across two routers yet.
        var reply = service.Ping(pc1Nic, IPv4Address.Parse("192.168.2.10"), count: 1).Replies.Single();
        Assert.False(reply.IsSuccess);
        Assert.Equal(PingReplyStatus.NoRoute, reply.Status);
    }
}
