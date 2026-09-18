using NetSim.Core.Arp;
using NetSim.Core.Devices;
using NetSim.Core.Ethernet;
using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Topology;

namespace NetSim.Core.Tests.Arp;

/// <summary>
/// Phase 20 integration (brief sections 46-49): the full "IPv4 destination -&gt; ARP lookup -&gt;
/// ARP request -&gt; ARP reply -&gt; ARP cache -&gt; known MAC" flow across one cable, driven through
/// the existing Phase 17 <see cref="EthernetTransmissionService"/> with every Ethernet/IPv4
/// abstraction left intact.
/// </summary>
public class ArpResolutionIntegrationTests
{
    private static readonly IPv4Address Pc0Ip = IPv4Address.Parse("192.168.1.10");
    private static readonly IPv4Address Pc1Ip = IPv4Address.Parse("192.168.1.20");
    private static readonly IPv4Address AbsentIp = IPv4Address.Parse("192.168.1.30");

    private sealed record Lab(
        Network Network,
        EthernetTransmissionService Ethernet,
        ArpLayer Arp,
        NetworkInterface Pc0,
        NetworkInterface Pc1);

    private static Lab BuildLab()
    {
        var network = new Network("Lab");
        var ethernet = new EthernetTransmissionService(new PacketEngine());
        var arp = new ArpLayer(new ArpProcessor());

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

        return new Lab(network, ethernet, arp, g0, g1);
    }

    private static EthernetFrame DeliveredFrame(EthernetTransmissionResult result) =>
        Assert.IsType<EthernetFrame>(result.Packet!.Payload);

    [Fact]
    public void FullResolution_Pc0LearnsPc1Mac_ViaBroadcastRequestAndUnicastReply()
    {
        var lab = BuildLab();

        // 1. PC0 wants to talk to 192.168.1.20 and has an empty ARP cache -> a request is needed.
        Assert.Equal(0, lab.Pc0.ArpCache.Count);
        var resolution = lab.Arp.Resolve(lab.Pc0, Pc1Ip);

        Assert.False(resolution.IsResolved);
        Assert.True(resolution.RequiresRequest);
        Assert.True(resolution.RequestFrame!.IsBroadcast);
        Assert.Equal(MacAddress.Broadcast, resolution.RequestFrame.DestinationMac);
        Assert.Equal(Pc1Ip, resolution.Request!.TargetProtocolAddress);
        Assert.Equal(lab.Pc0.MacAddress!.Value, resolution.Request.SenderHardwareAddress);

        // 2. The broadcast frame crosses the cable and reaches PC1.
        var toPc1 = lab.Ethernet.Transmit(lab.Network, lab.Pc0, resolution.RequestFrame);
        Assert.True(toPc1.IsSuccess);
        Assert.Same(lab.Pc1, toPc1.DestinationInterface);

        // 3. PC1 recognises it owns the target IP and builds a unicast reply to PC0.
        var pc1Report = lab.Arp.HandleIncoming(lab.Pc1, DeliveredFrame(toPc1));
        Assert.True(pc1Report.LocalInterfaceOwnsTarget);
        Assert.True(pc1Report.ReplyGenerated);
        Assert.Equal(lab.Pc0.MacAddress!.Value, pc1Report.ReplyFrame!.DestinationMac);
        Assert.False(pc1Report.ReplyFrame.IsBroadcast);

        // PC1 also learned PC0 from the request.
        Assert.True(lab.Pc1.ArpCache.TryResolve(Pc0Ip, out var pc0MacAtPc1));
        Assert.Equal(lab.Pc0.MacAddress!.Value, pc0MacAtPc1);

        // 4. The reply crosses back and PC0 learns 192.168.1.20 -> PC1's MAC.
        var toPc0 = lab.Ethernet.Transmit(lab.Network, lab.Pc1, pc1Report.ReplyFrame);
        Assert.True(toPc0.IsSuccess);
        Assert.Same(lab.Pc0, toPc0.DestinationInterface);

        var pc0Report = lab.Arp.HandleIncoming(lab.Pc0, DeliveredFrame(toPc0));
        Assert.Equal(ArpOperation.Reply, pc0Report.Operation);
        Assert.True(lab.Pc0.ArpCache.TryResolve(Pc1Ip, out var learned));
        Assert.Equal(lab.Pc1.MacAddress!.Value, learned);
    }

    [Fact]
    public void SecondResolution_AfterLearning_IsACacheHit_WithNoNewRequest()
    {
        var lab = BuildLab();
        lab.Pc0.ArpCache.AddOrUpdateDynamic(Pc1Ip, lab.Pc1.MacAddress!.Value);

        var requestsGenerated = 0;
        lab.Arp.RequestCreated += (_, _) => requestsGenerated++;

        var resolution = lab.Arp.Resolve(lab.Pc0, Pc1Ip);

        Assert.True(resolution.IsResolved);
        Assert.False(resolution.RequiresRequest);
        Assert.Equal(lab.Pc1.MacAddress!.Value, resolution.HardwareAddress);
        Assert.Equal(0, requestsGenerated);
    }

    [Fact]
    public void RequestForAnAddressNobodyOwns_GetsNoReply()
    {
        var lab = BuildLab();

        var resolution = lab.Arp.Resolve(lab.Pc0, AbsentIp);
        var toPc1 = lab.Ethernet.Transmit(lab.Network, lab.Pc0, resolution.RequestFrame!);
        var pc1Report = lab.Arp.HandleIncoming(lab.Pc1, DeliveredFrame(toPc1));

        Assert.True(pc1Report.IsSuccess);
        Assert.False(pc1Report.LocalInterfaceOwnsTarget);
        Assert.False(pc1Report.ReplyGenerated);
        Assert.False(lab.Pc0.ArpCache.Contains(AbsentIp));
    }

    [Fact]
    public void ArpRequestFrame_IsCarriedByTheEngineWithTheArpPayloadIntact()
    {
        var lab = BuildLab();
        var resolution = lab.Arp.Resolve(lab.Pc0, Pc1Ip);

        var toPc1 = lab.Ethernet.Transmit(lab.Network, lab.Pc0, resolution.RequestFrame!);

        var carriedFrame = DeliveredFrame(toPc1);
        Assert.Equal(EtherType.Arp, carriedFrame.EtherType);
        var carriedArp = Assert.IsType<ArpPacket>(carriedFrame.Payload);
        Assert.Equal(Pc1Ip, carriedArp.TargetProtocolAddress);
        Assert.Equal(Pc0Ip, carriedArp.SenderProtocolAddress);
        Assert.Equal(PacketProcessingOutcome.Delivered, new ArpProcessor().Process(toPc1.Packet!).Outcome);
    }
}
