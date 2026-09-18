using NetSim.Core.Devices;
using NetSim.Core.Ethernet;
using NetSim.Core.IP;
using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Topology;

namespace NetSim.Core.Tests.IP;

/// <summary>
/// Phase 19 integration (brief sections 52 / 53): an IPv6 packet exists as the payload of an
/// Ethernet frame, keyed by EtherType 0x86DD, and rides across one cable through the Phase 17
/// transmission service with every field preserved - alongside IPv4 on the same interface.
/// </summary>
public class EthernetIPv6IntegrationTests
{
    [Fact]
    public void IPv6Packet_LivesInsideAnEthernetFrame_WithEveryFieldPreserved()
    {
        var payload = RawPayload.FromText("application data");
        var ipPacket = IPv6Packet.Create(
            IPv6Address.Parse("2001:db8:1::10"),
            IPv6Address.Parse("2001:db8:1::20"),
            payload,
            NextHeader.Tcp,
            hopLimit: 64);

        var src = MacAddress.Parse("00:11:22:33:44:55");
        var dst = MacAddress.Parse("00:AA:BB:CC:DD:EE");
        var frame = EthernetFrame.Create(src, dst, EtherType.IPv6, ipPacket);

        Assert.Equal(EtherType.IPv6, frame.EtherType);
        Assert.Equal(0x86DD, frame.EtherType.Value);
        Assert.Same(ipPacket, frame.Payload);
        Assert.Same(ipPacket, frame.EncapsulatedPayload);

        var decapsulated = Assert.IsType<IPv6Packet>(frame.Payload);
        Assert.Equal(IPv6Address.Parse("2001:db8:1::10"), decapsulated.SourceAddress);
        Assert.Equal(IPv6Address.Parse("2001:db8:1::20"), decapsulated.DestinationAddress);
        Assert.Same(payload, decapsulated.Payload);

        Assert.True(frame.Validate().IsValid);
        Assert.Equal(EthernetFrame.HeaderSizeBytes + ipPacket.Length, frame.Length);
    }

    [Fact]
    public void IPv6OverEthernet_CrossesOneCable_ThroughTheTransmissionService()
    {
        var network = new Network("Lab");
        var packetEngine = new PacketEngine();
        var ethernet = new EthernetTransmissionService(packetEngine);
        var ipv6 = new IPv6Layer(new IPv6Processor());

        var pc0 = NetworkDeviceFactory.Create(DeviceType.Pc, "PC0");
        var pc1 = NetworkDeviceFactory.Create(DeviceType.Pc, "PC1");
        network.AddDevice(pc0);
        network.AddDevice(pc1);

        var g0 = pc0.Interfaces.Single(i => i.Name == "Ethernet0");
        var g1 = pc1.Interfaces.Single(i => i.Name == "Ethernet0");

        // Dual stack: both interfaces carry an IPv4 address and (a link-local plus a global) IPv6.
        g0.AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("192.168.1.10"), 24));
        g1.AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("192.168.1.20"), 24));
        g0.AddIPv6Configuration(Ipv6InterfaceConfiguration.Create(IPv6Address.Parse("fe80::10"), 64));
        g0.AddIPv6Configuration(Ipv6InterfaceConfiguration.Create(IPv6Address.Parse("2001:db8:1::10"), 64));
        g1.AddIPv6Configuration(Ipv6InterfaceConfiguration.Create(IPv6Address.Parse("2001:db8:1::20"), 64));

        network.Connect(g0, g1);
        g0.BringUp();
        g1.BringUp();

        var ipPacket = ipv6.CreatePacket(
            IPv6Address.Parse("2001:db8:1::10"),
            IPv6Address.Parse("2001:db8:1::20"),
            RawPayload.FromText("ping-ish"),
            NextHeader.Tcp);
        var frame = ipv6.Encapsulate(ipPacket, g0.MacAddress!.Value, g1.MacAddress!.Value);

        var result = ethernet.Transmit(network, g0, frame);

        Assert.True(result.IsSuccess);
        Assert.Same(g1, result.DestinationInterface);

        var carriedFrame = Assert.IsType<EthernetFrame>(result.Packet!.Payload);
        var carriedIp = Assert.IsType<IPv6Packet>(carriedFrame.Payload);
        Assert.Equal(IPv6Address.Parse("2001:db8:1::10"), carriedIp.SourceAddress);
        Assert.Equal(IPv6Address.Parse("2001:db8:1::20"), carriedIp.DestinationAddress);
        Assert.Equal(64, carriedIp.HopLimit);

        Assert.Equal(PacketProcessingOutcome.Delivered, ipv6.Process(result.Packet).Outcome);

        // IPv4 configuration is still intact on the interface the frame left from.
        Assert.Equal(IPv4Address.Parse("192.168.1.10"), g0.IPv4Address);
    }
}
