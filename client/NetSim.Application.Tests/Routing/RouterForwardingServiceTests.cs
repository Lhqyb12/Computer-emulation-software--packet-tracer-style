using System.Linq;
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
/// Phase 27 sections 18-22, 33, 55: one Layer 3 hop through a router carried out on the wire -
/// the outgoing frame is rebuilt with the router's own interface MAC, the next hop is resolved
/// through the real ARP engine (cache hit or miss), the IP endpoints and TTL behave per the brief,
/// and a drop generates the right ICMP error.
/// </summary>
public class RouterForwardingServiceTests
{
    private sealed record Lab(
        RouterForwardingService Service, Network Network, Router R1,
        NetworkInterface Ingress, NetworkInterface Egress, NetworkInterface Pc2Nic);

    private static Lab BuildLab()
    {
        var networkService = new NetworkService(new ApplicationState());
        var network = networkService.CreateNetwork("Lab");

        var r1 = (Router)NetworkDeviceFactory.Create(DeviceType.Router, "R1");
        var pc1 = NetworkDeviceFactory.Create(DeviceType.Pc, "PC1");
        var pc2 = NetworkDeviceFactory.Create(DeviceType.Pc, "PC2");
        network.AddDevice(r1);
        network.AddDevice(pc1);
        network.AddDevice(pc2);

        var gi0 = r1.Interfaces.First(i => i.Name == "GigabitEthernet0/0");
        var gi1 = r1.Interfaces.First(i => i.Name == "GigabitEthernet0/1");
        gi0.SetPrimaryIPv4Configuration(Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("192.168.1.1"), 24));
        gi1.SetPrimaryIPv4Configuration(Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("192.168.2.1"), 24));
        r1.SyncConnectedRoutes();

        var pc1Nic = pc1.Interfaces.Single();
        var pc2Nic = pc2.Interfaces.Single();
        pc1Nic.SetPrimaryIPv4Configuration(Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("192.168.1.10"), 24));
        pc2Nic.SetPrimaryIPv4Configuration(Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("192.168.2.10"), 24));

        Connect(network, pc1Nic, gi0);
        Connect(network, gi1, pc2Nic);

        var ethernet = new EthernetTransmissionService(new PacketEngine());
        var segment = new SwitchedSegmentService(ethernet, new SwitchingEngine());
        var service = new RouterForwardingService(
            new RoutingEngine(), new ArpLayer(new ArpProcessor()), new IPv4Layer(new IPv4Processor()),
            new IcmpLayer(new IcmpProcessor()), segment);

        return new Lab(service, network, r1, gi0, gi1, pc2Nic);
    }

    private static void Connect(Network net, NetworkInterface a, NetworkInterface b)
    {
        net.Connect(a, b);
        a.BringUp();
        b.BringUp();
    }

    private static IPv4Packet Packet(string src, string dst, byte ttl = 64, ProtocolNumber? protocol = null) =>
        IPv4Packet.Create(IPv4Address.Parse(src), IPv4Address.Parse(dst), protocol: protocol ?? ProtocolNumber.Icmp, timeToLive: ttl);

    [Fact]
    public void ForwardOneHop_RewritesLayer2_KeepsLayer3_AndDecrementsTtl_OnAnArpCacheMiss()
    {
        var lab = BuildLab();

        var result = lab.Service.ForwardOneHop(lab.Network, lab.R1, lab.Ingress, Packet("192.168.1.10", "192.168.2.10", ttl: 64));

        Assert.Equal(RouterForwardingOutcome.Forwarded, result.Outcome);
        Assert.Same(lab.Egress, result.EgressInterface);

        var frame = result.Delivery!.Frame;
        Assert.Equal(lab.Egress.MacAddress, frame.SourceMac);              // router's outgoing-interface MAC
        Assert.Equal(lab.Pc2Nic.MacAddress, frame.DestinationMac);         // resolved via ARP
        Assert.Equal(lab.Pc2Nic.Id, result.Delivery.DestinationInterface.Id);

        Assert.True(lab.Service is not null);
        var routed = (IPv4Packet)frame.Payload;
        Assert.Equal(IPv4Address.Parse("192.168.1.10"), routed.SourceAddress);   // IP endpoints unchanged
        Assert.Equal(IPv4Address.Parse("192.168.2.10"), routed.DestinationAddress);
        Assert.Equal((byte)63, routed.TimeToLive);                               // TTL 64 -> 63
    }

    [Fact]
    public void ForwardOneHop_UsesAnArpCacheHitWhenAvailable()
    {
        var lab = BuildLab();
        lab.Egress.ArpCache.AddOrUpdateDynamic(IPv4Address.Parse("192.168.2.10"), lab.Pc2Nic.MacAddress!.Value);

        var result = lab.Service.ForwardOneHop(lab.Network, lab.R1, lab.Ingress, Packet("192.168.1.10", "192.168.2.10"));

        Assert.Equal(RouterForwardingOutcome.Forwarded, result.Outcome);
        Assert.Equal(lab.Pc2Nic.MacAddress, result.NextHopMac);
    }

    [Fact]
    public void ForwardOneHop_PreservesANonIcmpPayloadProtocol()
    {
        var lab = BuildLab();

        var result = lab.Service.ForwardOneHop(
            lab.Network, lab.R1, lab.Ingress, Packet("192.168.1.10", "192.168.2.10", protocol: ProtocolNumber.Udp));

        Assert.Equal(RouterForwardingOutcome.Forwarded, result.Outcome);
        Assert.Equal(ProtocolNumber.Udp, ((IPv4Packet)result.Delivery!.Frame.Payload).Protocol);
    }

    [Fact]
    public void ForwardOneHop_NoRoute_DropsAndGeneratesIcmpDestinationUnreachable()
    {
        var lab = BuildLab();

        var result = lab.Service.ForwardOneHop(lab.Network, lab.R1, lab.Ingress, Packet("192.168.1.10", "10.10.10.10"));

        Assert.Equal(RouterForwardingOutcome.NoRoute, result.Outcome);
        Assert.Null(result.Delivery);
        Assert.NotNull(result.GeneratedIcmpError);
        Assert.True(result.GeneratedIcmpError!.IsDestinationUnreachable);
        Assert.Equal(IPv4Address.Parse("192.168.1.1"), result.GeneratedIcmpPacket!.SourceAddress);
        Assert.Equal(IPv4Address.Parse("192.168.1.10"), result.GeneratedIcmpPacket.DestinationAddress);
    }

    [Fact]
    public void ForwardOneHop_TtlExpired_DropsAndGeneratesIcmpTimeExceeded()
    {
        var lab = BuildLab();

        var result = lab.Service.ForwardOneHop(lab.Network, lab.R1, lab.Ingress, Packet("192.168.1.10", "192.168.2.10", ttl: 1));

        Assert.Equal(RouterForwardingOutcome.TtlExpired, result.Outcome);
        Assert.NotNull(result.GeneratedIcmpError);
        Assert.True(result.GeneratedIcmpError!.IsTimeExceeded);
    }

    [Fact]
    public void ForwardOneHop_PacketAddressedToTheRouter_IsLocalDelivery()
    {
        var lab = BuildLab();

        var result = lab.Service.ForwardOneHop(lab.Network, lab.R1, lab.Ingress, Packet("192.168.1.10", "192.168.2.1"));

        Assert.Equal(RouterForwardingOutcome.LocalDelivery, result.Outcome);
    }
}
