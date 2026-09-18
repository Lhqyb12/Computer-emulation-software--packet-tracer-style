using System.Collections.Generic;
using System.Linq;
using NetSim.Application.Dhcp;
using NetSim.Application.Diagnostics;
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
using NetSim.Core.Vlans;

namespace NetSim.Application.Tests.Switching;

/// <summary>
/// Phase 26 end-to-end: ARP, ICMP-Ping and DHCP stay inside their VLAN when a VLAN-aware switch
/// (or a trunked pair of switches) sits between the endpoints - same-VLAN traffic works exactly as
/// before, cross-VLAN traffic has no Layer 2 path (there is no inter-VLAN routing in this phase).
/// </summary>
public class VlanIsolationIntegrationTests
{
    private static readonly VlanId Vlan10 = new(10);
    private static readonly VlanId Vlan20 = new(20);

    private static NetworkInterface Nic(NetworkDevice d) => d.Interfaces.Single();

    private static NetworkInterface Port(NetworkDevice sw, int n) => sw.Interfaces.First(p => p.Name == $"FastEthernet0/{n}");

    private static void ConnectUp(Network net, NetworkInterface a, NetworkInterface b)
    {
        net.Connect(a, b);
        a.BringUp();
        b.BringUp();
    }

    private static PingService NewPingService(EthernetTransmissionService ethernet, INetworkService networkService, ISwitchedSegmentService segment) =>
        new(new IcmpLayer(new IcmpProcessor()), new IPv4Layer(new IPv4Processor()),
            new ArpLayer(new ArpProcessor()), ethernet, networkService, segment);

    // ---- One switch, three PCs (brief section 27) ------------------------------------------

    private sealed record ThreePcLab(
        Network Network, NetworkService NetworkService, EthernetTransmissionService Ethernet,
        ISwitchedSegmentService Segment, NetworkDevice Pc1, NetworkDevice Pc2, NetworkDevice Pc3);

    private static ThreePcLab BuildThreePcLab()
    {
        var networkService = new NetworkService(new ApplicationState());
        var network = networkService.CreateNetwork("Lab");

        var sw = (Switch)NetworkDeviceFactory.Create(DeviceType.Switch, "SW1");
        var pc1 = NetworkDeviceFactory.Create(DeviceType.Pc, "PC1");
        var pc2 = NetworkDeviceFactory.Create(DeviceType.Pc, "PC2");
        var pc3 = NetworkDeviceFactory.Create(DeviceType.Pc, "PC3");
        foreach (var d in new[] { sw, pc1, pc2, pc3 })
        {
            network.AddDevice(d);
        }

        Nic(pc1).AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("192.168.1.10"), 24));
        Nic(pc2).AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("192.168.1.11"), 24));
        Nic(pc3).AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("192.168.1.12"), 24));

        ConnectUp(network, Nic(pc1), Port(sw, 1));
        ConnectUp(network, Nic(pc2), Port(sw, 2));
        ConnectUp(network, Nic(pc3), Port(sw, 3));

        sw.CreateVlan(Vlan10);
        sw.CreateVlan(Vlan20);
        sw.ConfigureAccessPort(Port(sw, 1), Vlan10);
        sw.ConfigureAccessPort(Port(sw, 2), Vlan10);
        sw.ConfigureAccessPort(Port(sw, 3), Vlan20);

        var ethernet = new EthernetTransmissionService(new PacketEngine());
        var segment = new SwitchedSegmentService(ethernet, new SwitchingEngine(new ManualSimulationClock()));
        return new ThreePcLab(network, networkService, ethernet, segment, pc1, pc2, pc3);
    }

    [Fact]
    public void Ping_SameVlan_Succeeds()
    {
        var lab = BuildThreePcLab();
        var ping = NewPingService(lab.Ethernet, lab.NetworkService, lab.Segment);

        var result = ping.Ping(Nic(lab.Pc1), IPv4Address.Parse("192.168.1.11"), count: 2);

        Assert.True(result.IsFullSuccess);
    }

    [Fact]
    public void Ping_DifferentVlans_FailsAtLayer2_WithNoRouting()
    {
        var lab = BuildThreePcLab();
        var ping = NewPingService(lab.Ethernet, lab.NetworkService, lab.Segment);

        var result = ping.Ping(Nic(lab.Pc1), IPv4Address.Parse("192.168.1.12"), count: 2);

        Assert.False(result.IsFullSuccess);
        Assert.Equal(result.PacketsSent, result.PacketsLost);
    }

    [Fact]
    public void Arp_RequestStaysInsideTheVlan_TheOtherVlanHostNeverSeesIt()
    {
        var lab = BuildThreePcLab();
        var arp = new ArpLayer(new ArpProcessor());
        var pc3Ip = Nic(lab.Pc3).PrimaryIPv4Configuration!.Address;

        var resolution = arp.Resolve(Nic(lab.Pc1), pc3Ip); // VLAN 10 host asks for a VLAN 20 host
        Assert.False(resolution.IsResolved);

        var requestOut = lab.Segment.TransmitAcrossSegment(lab.Network, Nic(lab.Pc1), resolution.RequestFrame!);

        Assert.DoesNotContain(requestOut.Deliveries, d => d.DestinationInterface.Id == Nic(lab.Pc3).Id);
        Assert.Contains(requestOut.Deliveries, d => d.DestinationInterface.Id == Nic(lab.Pc2).Id); // same-VLAN host does see it
    }

    // ---- DHCP isolation (brief section 43) ------------------------------------------------

    [Fact]
    public void Dhcp_DiscoverReachesTheServerInTheSameVlan_ButNotAClientInAnotherVlan()
    {
        var networkService = new NetworkService(new ApplicationState());
        var network = networkService.CreateNetwork("Lab");

        var sw = (Switch)NetworkDeviceFactory.Create(DeviceType.Switch, "SW1");
        var pcSameVlan = NetworkDeviceFactory.Create(DeviceType.Pc, "PC-v10");
        var pcOtherVlan = NetworkDeviceFactory.Create(DeviceType.Pc, "PC-v20");
        var server = NetworkDeviceFactory.Create(DeviceType.Server, "DhcpServer");
        foreach (var d in new[] { sw, pcSameVlan, pcOtherVlan, server })
        {
            network.AddDevice(d);
        }

        var serverIp = IPv4Address.Parse("192.168.10.1");
        Nic(server).AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(serverIp, 24));
        ConnectUp(network, Nic(pcSameVlan), Port(sw, 1));
        ConnectUp(network, Nic(pcOtherVlan), Port(sw, 2));
        ConnectUp(network, Nic(server), Port(sw, 3));

        sw.CreateVlan(Vlan10);
        sw.CreateVlan(Vlan20);
        sw.ConfigureAccessPort(Port(sw, 1), Vlan10); // client in VLAN 10 (with the server)
        sw.ConfigureAccessPort(Port(sw, 2), Vlan20); // client in VLAN 20 (isolated)
        sw.ConfigureAccessPort(Port(sw, 3), Vlan10); // server in VLAN 10

        var ethernet = new EthernetTransmissionService(new PacketEngine());
        var udpDelivery = new UdpDeliveryManager();
        var udp = new UdpLayer(new UdpProcessor(), udpDelivery);
        var ipv4 = new IPv4Layer(new IPv4Processor());
        var time = TestTimeProvider.StartingAtEpoch();
        var serverService = new DhcpServerService(udpDelivery);
        var segment = new SwitchedSegmentService(ethernet, new SwitchingEngine(new ManualSimulationClock()));

        var config = new DhcpServerConfiguration(
            serverIp, 24, IPv4Address.Parse("192.168.10.100"), IPv4Address.Parse("192.168.10.150"),
            defaultGateway: serverIp, dnsServer: serverIp, leaseDuration: TimeSpan.FromSeconds(3600));
        serverService.Start(server, new DhcpServer(config, time));

        DhcpClient NewClient() => new(
            udp, ipv4, ethernet, networkService, serverService, new DnsClientConfigurationStore(),
            new DhcpClientStateStore(), new SequentialDhcpTransactionIdSource(), time, segment);

        // Same VLAN as the server -> a lease is bound.
        var sameVlanResult = NewClient().Acquire(Nic(pcSameVlan));
        Assert.True(sameVlanResult.IsSuccess);
        Assert.Equal(DhcpAcquireStatus.Bound, sameVlanResult.Status);

        // Different VLAN -> the DISCOVER broadcast never reaches the server; no lease.
        var otherVlanResult = NewClient().Acquire(Nic(pcOtherVlan));
        Assert.False(otherVlanResult.IsSuccess);
    }

    // ---- Full acceptance scenario (brief section 51) ------------------------------------

    [Fact]
    public void EndToEnd_TwoSwitchesOneTrunk_FourPcs_OnlySameVlanPairsCommunicate()
    {
        var networkService = new NetworkService(new ApplicationState());
        var network = networkService.CreateNetwork("Campus");

        var sw1 = (Switch)NetworkDeviceFactory.Create(DeviceType.Switch, "SW1");
        var sw2 = (Switch)NetworkDeviceFactory.Create(DeviceType.Switch, "SW2");
        var pc1 = NetworkDeviceFactory.Create(DeviceType.Pc, "PC1"); // SW1 VLAN 10
        var pc2 = NetworkDeviceFactory.Create(DeviceType.Pc, "PC2"); // SW1 VLAN 20
        var pc3 = NetworkDeviceFactory.Create(DeviceType.Pc, "PC3"); // SW2 VLAN 10
        var pc4 = NetworkDeviceFactory.Create(DeviceType.Pc, "PC4"); // SW2 VLAN 20
        foreach (var d in new NetworkDevice[] { sw1, sw2, pc1, pc2, pc3, pc4 })
        {
            network.AddDevice(d);
        }

        Nic(pc1).AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("192.168.10.1"), 24));
        Nic(pc3).AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("192.168.10.3"), 24));
        Nic(pc2).AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("192.168.20.2"), 24));
        Nic(pc4).AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("192.168.20.4"), 24));

        ConnectUp(network, Nic(pc1), Port(sw1, 1));
        ConnectUp(network, Nic(pc2), Port(sw1, 2));
        ConnectUp(network, Port(sw1, 8), Port(sw2, 8)); // trunk
        ConnectUp(network, Nic(pc3), Port(sw2, 1));
        ConnectUp(network, Nic(pc4), Port(sw2, 2));

        foreach (var sw in new[] { sw1, sw2 })
        {
            sw.CreateVlan(Vlan10);
            sw.CreateVlan(Vlan20);
            sw.ConfigureAccessPort(Port(sw, 1), Vlan10);
            sw.ConfigureAccessPort(Port(sw, 2), Vlan20);
            sw.ConfigureTrunkPort(Port(sw, 8), nativeVlan: VlanId.Default, allowedVlans: [Vlan10, Vlan20]);
        }

        var ethernet = new EthernetTransmissionService(new PacketEngine());
        var segment = new SwitchedSegmentService(ethernet, new SwitchingEngine(new ManualSimulationClock()));
        var ping = NewPingService(ethernet, networkService, segment);

        Assert.True(ping.Ping(Nic(pc1), IPv4Address.Parse("192.168.10.3"), count: 2).IsFullSuccess);  // PC1 <-> PC3 (VLAN 10)
        Assert.True(ping.Ping(Nic(pc2), IPv4Address.Parse("192.168.20.4"), count: 2).IsFullSuccess);  // PC2 <-> PC4 (VLAN 20)

        Assert.False(ping.Ping(Nic(pc1), IPv4Address.Parse("192.168.20.2"), count: 1).IsFullSuccess); // PC1 -> PC2
        Assert.False(ping.Ping(Nic(pc1), IPv4Address.Parse("192.168.20.4"), count: 1).IsFullSuccess); // PC1 -> PC4
        Assert.False(ping.Ping(Nic(pc2), IPv4Address.Parse("192.168.10.3"), count: 1).IsFullSuccess); // PC2 -> PC3
    }

    [Fact]
    public void BackwardCompatible_ASwitchWithNoVlanConfiguration_BehavesAsOneFlatBroadcastDomain()
    {
        var networkService = new NetworkService(new ApplicationState());
        var network = networkService.CreateNetwork("Legacy");

        var sw = NetworkDeviceFactory.Create(DeviceType.Switch, "SW1");
        var pc1 = NetworkDeviceFactory.Create(DeviceType.Pc, "PC1");
        var pc2 = NetworkDeviceFactory.Create(DeviceType.Pc, "PC2");
        foreach (var d in new[] { sw, pc1, pc2 })
        {
            network.AddDevice(d);
        }

        Nic(pc1).AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("10.0.0.1"), 24));
        Nic(pc2).AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("10.0.0.2"), 24));
        ConnectUp(network, Nic(pc1), Port(sw, 1));
        ConnectUp(network, Nic(pc2), Port(sw, 2));
        // No VLANs configured at all - every port is still access VLAN 1.

        var ethernet = new EthernetTransmissionService(new PacketEngine());
        var segment = new SwitchedSegmentService(ethernet, new SwitchingEngine(new ManualSimulationClock()));
        var ping = NewPingService(ethernet, networkService, segment);

        Assert.True(ping.Ping(Nic(pc1), IPv4Address.Parse("10.0.0.2"), count: 2).IsFullSuccess);
    }
}
