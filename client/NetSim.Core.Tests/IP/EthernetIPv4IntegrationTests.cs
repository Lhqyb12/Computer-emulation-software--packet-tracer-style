using NetSim.Core.Devices;
using NetSim.Core.Ethernet;
using NetSim.Core.IP;
using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Topology;

namespace NetSim.Core.Tests.IP;

/// <summary>
/// Phase 18 integration (brief section 51): an IPv4 packet exists as the payload of an Ethernet
/// frame, keyed by EtherType, and rides across one cable through the Phase 17 transmission service
/// with every field preserved.
/// </summary>
public class EthernetIPv4IntegrationTests
{
    [Fact]
    public void IPv4Packet_LivesInsideAnEthernetFrame_WithEveryFieldPreserved()
    {
        var payload = RawPayload.FromText("application data");
        var ipPacket = IPv4Packet.Create(
            IPv4Address.Parse("192.168.1.10"),
            IPv4Address.Parse("192.168.1.20"),
            payload,
            ProtocolNumber.Tcp,
            timeToLive: 64);

        var src = MacAddress.Parse("00:11:22:33:44:55");
        var dst = MacAddress.Parse("00:AA:BB:CC:DD:EE");
        var frame = EthernetFrame.Create(src, dst, EtherType.IPv4, ipPacket);

        // EtherType identifies IPv4 and the encapsulation chain is intact.
        Assert.Equal(EtherType.IPv4, frame.EtherType);
        Assert.True(frame.EtherType == EtherType.IPv4);
        Assert.Same(ipPacket, frame.Payload);
        Assert.Same(ipPacket, frame.EncapsulatedPayload);

        // The decapsulated IPv4 packet still carries its addresses and payload.
        var decapsulated = Assert.IsType<IPv4Packet>(frame.Payload);
        Assert.Equal(IPv4Address.Parse("192.168.1.10"), decapsulated.SourceAddress);
        Assert.Equal(IPv4Address.Parse("192.168.1.20"), decapsulated.DestinationAddress);
        Assert.Same(payload, decapsulated.Payload);

        // The Ethernet structure remains valid and its length aggregates the IPv4 packet.
        Assert.True(frame.Validate().IsValid);
        Assert.Equal(EthernetFrame.HeaderSizeBytes + ipPacket.Length, frame.Length);
    }

    [Fact]
    public void IPv4OverEthernet_CrossesOneCable_ThroughTheTransmissionService()
    {
        var network = new Network("Lab");
        var packetEngine = new PacketEngine();
        var ethernet = new EthernetTransmissionService(packetEngine);
        var ipv4 = new IPv4Layer(new IPv4Processor());

        var pc0 = NetworkDeviceFactory.Create(DeviceType.Pc, "PC0");
        var pc1 = NetworkDeviceFactory.Create(DeviceType.Pc, "PC1");
        network.AddDevice(pc0);
        network.AddDevice(pc1);

        var g0 = pc0.Interfaces.Single(i => i.Name == "Ethernet0");
        var g1 = pc1.Interfaces.Single(i => i.Name == "Ethernet0");
        g0.AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("192.168.1.10"), 24));
        g1.AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("192.168.1.20"), 24));
        network.Connect(g0, g1);
        g0.BringUp();
        g1.BringUp();

        var ipPacket = ipv4.CreatePacket(
            g0.IPv4Address!.Value, g1.IPv4Address!.Value, RawPayload.FromText("ping-ish"), ProtocolNumber.Tcp);
        var frame = ipv4.Encapsulate(ipPacket, g0.MacAddress!.Value, g1.MacAddress!.Value);

        var result = ethernet.Transmit(network, g0, frame);

        Assert.True(result.IsSuccess);
        Assert.Same(g1, result.DestinationInterface);

        // The tracked packet carries the Ethernet frame, which carries the untouched IPv4 packet.
        var carriedFrame = Assert.IsType<EthernetFrame>(result.Packet!.Payload);
        var carriedIp = Assert.IsType<IPv4Packet>(carriedFrame.Payload);
        Assert.Equal(IPv4Address.Parse("192.168.1.10"), carriedIp.SourceAddress);
        Assert.Equal(IPv4Address.Parse("192.168.1.20"), carriedIp.DestinationAddress);
        Assert.Equal(64, carriedIp.TimeToLive);

        // And the IPv4 processor accepts it out of that same tracked packet.
        Assert.Equal(PacketProcessingOutcome.Delivered, ipv4.Process(result.Packet).Outcome);
    }
}
