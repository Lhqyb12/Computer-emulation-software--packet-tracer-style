using System.Linq;
using NetSim.Application.Diagnostics;
using NetSim.Application.Routing;
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
/// Phase 28 sections 32-40, 44: the end-to-end acceptance scenario. Two routers, static routes,
/// real Core engines (ARP / ICMP / IPv4 / Ethernet / switching / routing) -
/// <c>PC1 -&gt; R1 -&gt; R2 -&gt; PC2</c> and back. Also the default-route, ARP-next-hop,
/// interface-down and route-removal acceptance tests.
/// </summary>
public class TwoRouterStaticRoutingIntegrationTests
{
    private static readonly IPv4Address Pc1Ip = IPv4Address.Parse("192.168.1.10");
    private static readonly IPv4Address Pc2Ip = IPv4Address.Parse("192.168.2.10");
    private static readonly IPv4Address R1Lan = IPv4Address.Parse("192.168.1.1");
    private static readonly IPv4Address R1Link = IPv4Address.Parse("10.0.0.1");
    private static readonly IPv4Address R2Link = IPv4Address.Parse("10.0.0.2");
    private static readonly IPv4Address R2Lan = IPv4Address.Parse("192.168.2.1");

    private sealed record Lab(
        PingService Ping, StaticRouteService StaticRoutes, Network Network,
        Router R1, Router R2, NetworkInterface Pc1Nic, NetworkInterface Pc2Nic,
        NetworkInterface R1LinkNic, NetworkInterface R2LinkNic);

    private static void ConnectUp(Network net, NetworkInterface a, NetworkInterface b)
    {
        net.Connect(a, b);
        a.BringUp();
        b.BringUp();
    }

    // PC1(192.168.1.10) - R1[Gi0/0 .1.1 | Gi0/1 10.0.0.1/30] --- [Gi0/0 10.0.0.2/30 | Gi0/1 .2.1]R2 - PC2(192.168.2.10)
    private static Lab BuildLab(bool addStaticRoutes = true)
    {
        var state = new ApplicationState();
        var networkService = new NetworkService(state);
        var network = networkService.CreateNetwork("Lab");

        var pc1 = NetworkDeviceFactory.Create(DeviceType.Pc, "PC1");
        var pc2 = NetworkDeviceFactory.Create(DeviceType.Pc, "PC2");
        var r1 = (Router)NetworkDeviceFactory.Create(DeviceType.Router, "R1");
        var r2 = (Router)NetworkDeviceFactory.Create(DeviceType.Router, "R2");
        foreach (var d in new NetworkDevice[] { pc1, pc2, r1, r2 })
        {
            network.AddDevice(d);
        }

        var pc1Nic = pc1.Interfaces.Single();
        var pc2Nic = pc2.Interfaces.Single();
        var r1Lan = r1.Interfaces.First(i => i.Name == "GigabitEthernet0/0");
        var r1Link = r1.Interfaces.First(i => i.Name == "GigabitEthernet0/1");
        var r2Link = r2.Interfaces.First(i => i.Name == "GigabitEthernet0/0");
        var r2Lan = r2.Interfaces.First(i => i.Name == "GigabitEthernet0/1");

        pc1Nic.SetPrimaryIPv4Configuration(Ipv4InterfaceConfiguration.Create(Pc1Ip, 24));
        pc2Nic.SetPrimaryIPv4Configuration(Ipv4InterfaceConfiguration.Create(Pc2Ip, 24));
        r1Lan.SetPrimaryIPv4Configuration(Ipv4InterfaceConfiguration.Create(R1Lan, 24));
        r1Link.SetPrimaryIPv4Configuration(Ipv4InterfaceConfiguration.Create(R1Link, 30));
        r2Link.SetPrimaryIPv4Configuration(Ipv4InterfaceConfiguration.Create(R2Link, 30));
        r2Lan.SetPrimaryIPv4Configuration(Ipv4InterfaceConfiguration.Create(R2Lan, 24));
        r1.SyncConnectedRoutes();
        r2.SyncConnectedRoutes();

        pc1Nic.SetIPv4DefaultGateway(R1Lan);
        pc2Nic.SetIPv4DefaultGateway(R2Lan);

        ConnectUp(network, pc1Nic, r1Lan);
        ConnectUp(network, r1Link, r2Link);
        ConnectUp(network, r2Lan, pc2Nic);

        var ethernet = new EthernetTransmissionService(new PacketEngine());
        var segment = new SwitchedSegmentService(ethernet, new SwitchingEngine());
        var ping = new PingService(
            new IcmpLayer(new IcmpProcessor()), new IPv4Layer(new IPv4Processor()),
            new ArpLayer(new ArpProcessor()), ethernet, networkService, segment);
        var staticRoutes = new StaticRouteService(state);

        if (addStaticRoutes)
        {
            Assert.True(staticRoutes.AddStaticRoute(r1, new StaticRouteInput
            { DestinationNetwork = "192.168.2.0", PrefixLength = 24, NextHop = "10.0.0.2" }).IsSuccess);
            Assert.True(staticRoutes.AddStaticRoute(r2, new StaticRouteInput
            { DestinationNetwork = "192.168.1.0", PrefixLength = 24, NextHop = "10.0.0.1" }).IsSuccess);
        }

        return new Lab(ping, staticRoutes, network, r1, r2, pc1Nic, pc2Nic, r1Link, r2Link);
    }

    [Fact]
    public void Ping_Pc1_To_Pc2_AcrossTwoRouters_Succeeds_WithStaticRoutes()
    {
        var lab = BuildLab();

        var result = lab.Ping.Ping(lab.Pc1Nic, Pc2Ip, count: 3);

        Assert.Equal(3, result.PacketsReceived);
        Assert.True(result.IsFullSuccess);
        Assert.All(result.Replies, r => Assert.Equal(Pc2Ip, r.RespondingAddress));
    }

    [Fact]
    public void Ping_AcrossTwoRouters_DecrementsTtlOncePerRouter()
    {
        var lab = BuildLab();

        var reply = lab.Ping.Ping(lab.Pc1Nic, Pc2Ip, count: 1).Replies.Single();

        Assert.True(reply.IsSuccess);
        // PC2 emits the Echo Reply at TTL 64; it crosses R2 then R1 on the way home.
        Assert.Equal((byte)(IPv4Packet.DefaultTimeToLive - 2), reply.TimeToLive);
    }

    [Fact]
    public void WithoutStaticRoutes_Pc1CannotReachPc2()
    {
        var lab = BuildLab(addStaticRoutes: false);

        var reply = lab.Ping.Ping(lab.Pc1Nic, Pc2Ip, count: 1).Replies.Single();

        Assert.False(reply.IsSuccess);
        Assert.Equal(PingReplyStatus.NoRoute, reply.Status);
    }

    [Fact]
    public void Ping_ResolvesArpForTheNextHopRouter_NotTheFinalDestination()
    {
        var lab = BuildLab();

        lab.Ping.Ping(lab.Pc1Nic, Pc2Ip, count: 1);

        // R1 forwarded toward its static route's next hop (10.0.0.2), so its link interface learned
        // the MAC of 10.0.0.2 - and never ARPed for the remote 192.168.2.10.
        Assert.True(lab.R1LinkNic.ArpCache.TryGet(R2Link, out _));
        Assert.False(lab.R1LinkNic.ArpCache.TryGet(Pc2Ip, out _));
    }

    [Fact]
    public void DefaultStaticRoute_IsUsedOnlyWhenNoMoreSpecificRouteExists()
    {
        var lab = BuildLab(addStaticRoutes: false);
        // R1: only a default route toward R2; R2: a route back to PC1's LAN.
        lab.StaticRoutes.AddStaticRoute(lab.R1, new StaticRouteInput
        { DestinationNetwork = "0.0.0.0", PrefixLength = 0, NextHop = "10.0.0.2" });
        lab.StaticRoutes.AddStaticRoute(lab.R2, new StaticRouteInput
        { DestinationNetwork = "192.168.1.0", PrefixLength = 24, NextHop = "10.0.0.1" });

        // 192.168.2.10 has no specific route on R1 -> the default route carries it.
        var viaDefault = lab.R1.RoutingTable.FindBestRoute(Pc2Ip);
        Assert.True(viaDefault.IsResolvable);
        Assert.True(viaDefault.Route!.IsDefault);
        Assert.True(lab.Ping.Ping(lab.Pc1Nic, Pc2Ip, count: 1).Replies.Single().IsSuccess);

        // Add a specific route; it must now win over the default.
        lab.StaticRoutes.AddStaticRoute(lab.R1, new StaticRouteInput
        { DestinationNetwork = "192.168.2.0", PrefixLength = 24, NextHop = "10.0.0.2" });
        var specific = lab.R1.RoutingTable.FindBestRoute(Pc2Ip);
        Assert.False(specific.Route!.IsDefault);
        Assert.Equal(24, specific.PrefixLength);
    }

    [Fact]
    public void BringingTheTransitInterfaceDown_MakesTheRouteUnusable_ThenUpRestoresIt_WithoutRecreatingIt()
    {
        var lab = BuildLab();
        Assert.True(lab.Ping.Ping(lab.Pc1Nic, Pc2Ip, count: 1).Replies.Single().IsSuccess);

        lab.R1LinkNic.BringDown();

        Assert.Equal(StaticRouteStatus.UnreachableNextHop, lab.StaticRoutes.GetStaticRoutes(lab.R1).Single().Status);
        Assert.False(lab.Ping.Ping(lab.Pc1Nic, Pc2Ip, count: 1).Replies.Single().IsSuccess);
        Assert.Single(lab.StaticRoutes.GetStaticRoutes(lab.R1));   // still configured

        lab.R1LinkNic.BringUp();

        Assert.Equal(StaticRouteStatus.Active, lab.StaticRoutes.GetStaticRoutes(lab.R1).Single().Status);
        Assert.True(lab.Ping.Ping(lab.Pc1Nic, Pc2Ip, count: 1).Replies.Single().IsSuccess);
    }

    [Fact]
    public void RemovingR1sStaticRoute_BreaksForwarding_WithNoRoute()
    {
        var lab = BuildLab();
        Assert.True(lab.Ping.Ping(lab.Pc1Nic, Pc2Ip, count: 1).Replies.Single().IsSuccess);

        var routeId = lab.StaticRoutes.GetStaticRoutes(lab.R1).Single().Id;
        Assert.True(lab.StaticRoutes.RemoveStaticRoute(lab.R1, routeId).IsSuccess);

        Assert.Empty(lab.StaticRoutes.GetStaticRoutes(lab.R1));
        var reply = lab.Ping.Ping(lab.Pc1Nic, Pc2Ip, count: 1).Replies.Single();
        Assert.False(reply.IsSuccess);
        Assert.Equal(PingReplyStatus.NoRoute, reply.Status);
    }

    [Fact]
    public void TcpTraffic_TraversesBothRouters_WithEndpointsPreserved_AndTtlDecrementedPerHop() =>
        AssertTransportTraverses(ProtocolNumber.Tcp);

    [Fact]
    public void UdpTraffic_TraversesBothRouters_WithEndpointsPreserved_AndTtlDecrementedPerHop() =>
        AssertTransportTraverses(ProtocolNumber.Udp);

    private static void AssertTransportTraverses(ProtocolNumber protocol)
    {
        var lab = BuildLab();
        var engine = new RoutingEngine();
        var original = IPv4Packet.Create(Pc1Ip, Pc2Ip, protocol: protocol, timeToLive: 64);

        // Hop 1: R1 (ingress on its LAN interface).
        var r1Lan = lab.R1.Interfaces.First(i => i.Name == "GigabitEthernet0/0");
        var atR1 = engine.Route(lab.R1, r1Lan, original);
        Assert.Equal(RoutingOutcome.Forward, atR1.Outcome);
        Assert.Same(lab.R1LinkNic, atR1.OutgoingInterface);
        Assert.Equal(R2Link, atR1.NextHop);
        Assert.Equal((byte)63, atR1.NewTimeToLive);

        // Hop 2: R2 (ingress on its link interface).
        var atR2 = engine.Route(lab.R2, lab.R2LinkNic, atR1.ForwardedPacket!);
        Assert.Equal(RoutingOutcome.Forward, atR2.Outcome);
        Assert.Equal(RouteType.Connected, atR2.Route!.Type);
        Assert.Equal((byte)62, atR2.NewTimeToLive);

        var delivered = atR2.ForwardedPacket!;
        Assert.Equal(Pc1Ip, delivered.SourceAddress);
        Assert.Equal(Pc2Ip, delivered.DestinationAddress);
        Assert.Equal(protocol, delivered.Protocol);
    }
}
