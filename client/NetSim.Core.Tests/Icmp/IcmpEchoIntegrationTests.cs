using System.Linq;
using NetSim.Core.Arp;
using NetSim.Core.Devices;
using NetSim.Core.Ethernet;
using NetSim.Core.Icmp;
using NetSim.Core.IP;
using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Topology;

namespace NetSim.Core.Tests.Icmp;

/// <summary>
/// Phase 21 integration (brief sections 4 &amp; 41-46): the full "PC0 pings PC1" flow - Echo Request
/// -&gt; IPv4 -&gt; ARP (if needed) -&gt; Ethernet -&gt; PC1 recognises it owns the destination -&gt; Echo
/// Reply -&gt; IPv4 -&gt; Ethernet -&gt; back to PC0 - driven through the real Phase 17-21 engines with
/// every abstraction left intact, mirroring <c>ArpResolutionIntegrationTests</c>.
/// </summary>
public class IcmpEchoIntegrationTests
{
    private static readonly IPv4Address Pc0Ip = IPv4Address.Parse("192.168.1.10");
    private static readonly IPv4Address Pc1Ip = IPv4Address.Parse("192.168.1.20");
    private static readonly IPv4Address AbsentIp = IPv4Address.Parse("192.168.1.30");

    private sealed record Lab(
        Network Network,
        EthernetTransmissionService Ethernet,
        IPv4Layer Ipv4,
        ArpLayer Arp,
        IcmpLayer Icmp,
        NetworkInterface Pc0,
        NetworkInterface Pc1);

    private static Lab BuildLab()
    {
        var network = new Network("Lab");
        var ethernet = new EthernetTransmissionService(new PacketEngine());
        var ipv4 = new IPv4Layer(new IPv4Processor());
        var arp = new ArpLayer(new ArpProcessor());
        var icmp = new IcmpLayer(new IcmpProcessor());

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

        return new Lab(network, ethernet, ipv4, arp, icmp, g0, g1);
    }

    private static EthernetFrame DeliveredFrame(EthernetTransmissionResult result) =>
        Assert.IsType<EthernetFrame>(result.Packet!.Payload);

    /// <summary>Resolves PC1's MAC from PC0 (broadcast request/reply round trip on a cache miss), mirroring what <c>PingService</c> does.</summary>
    private static MacAddress ResolvePc1Mac(Lab lab)
    {
        var resolution = lab.Arp.Resolve(lab.Pc0, Pc1Ip);
        if (resolution.IsResolved)
        {
            return resolution.HardwareAddress!.Value;
        }

        var toPc1 = lab.Ethernet.Transmit(lab.Network, lab.Pc0, resolution.RequestFrame!);
        var pc1Report = lab.Arp.HandleIncoming(lab.Pc1, DeliveredFrame(toPc1));
        var toPc0 = lab.Ethernet.Transmit(lab.Network, lab.Pc1, pc1Report.ReplyFrame!);
        lab.Arp.HandleIncoming(lab.Pc0, DeliveredFrame(toPc0));

        Assert.True(lab.Pc0.ArpCache.TryResolve(Pc1Ip, out var mac));
        return mac;
    }

    [Fact]
    public void FullPing_Pc0ToPc1_ArpCacheMiss_EchoRequestThenEchoReply_Succeeds()
    {
        var lab = BuildLab();
        Assert.Equal(0, lab.Pc0.ArpCache.Count);

        // 1. ARP resolves PC1's MAC via a broadcast request / unicast reply round trip.
        var pc1Mac = ResolvePc1Mac(lab);
        Assert.Equal(lab.Pc1.MacAddress!.Value, pc1Mac);

        // 2. Build the Echo Request, wrap in IPv4 (protocol ICMP), wrap in Ethernet, send.
        var echoRequest = lab.Icmp.CreateEchoRequest(1234, 1, RawPayload.FromText("Hello"));
        var requestIp = lab.Icmp.Encapsulate(echoRequest, Pc0Ip, Pc1Ip);
        Assert.Equal(ProtocolNumber.Icmp, requestIp.Protocol);

        var requestFrame = lab.Ipv4.Encapsulate(requestIp, lab.Pc0.MacAddress!.Value, pc1Mac);
        var toPc1 = lab.Ethernet.Transmit(lab.Network, lab.Pc0, requestFrame);
        Assert.True(toPc1.IsSuccess);
        Assert.Same(lab.Pc1, toPc1.DestinationInterface);

        // 3. PC1 recognises it owns 192.168.1.20 and builds an Echo Reply.
        var deliveredRequestIp = Assert.IsType<IPv4Packet>(DeliveredFrame(toPc1).Payload);
        var echoReport = lab.Icmp.HandleIncoming(lab.Pc1, deliveredRequestIp);
        Assert.True(echoReport.LocalInterfaceOwnsDestination);
        Assert.True(echoReport.ReplyGenerated);
        Assert.Equal(1234, echoReport.Reply!.Identifier);
        Assert.Equal(1, echoReport.Reply.SequenceNumber);
        Assert.Equal("Hello", System.Text.Encoding.UTF8.GetString(echoReport.Reply.Data.Data.Span));

        // 4. Send the reply back (PC1 already learned PC0's MAC from the ARP request above).
        var replyFrame = lab.Ipv4.Encapsulate(echoReport.ReplyPacket!, lab.Pc1.MacAddress!.Value, lab.Pc0.MacAddress!.Value);
        var toPc0 = lab.Ethernet.Transmit(lab.Network, lab.Pc1, replyFrame);
        Assert.True(toPc0.IsSuccess);
        Assert.Same(lab.Pc0, toPc0.DestinationInterface);

        var deliveredReplyIp = Assert.IsType<IPv4Packet>(DeliveredFrame(toPc0).Payload);
        var replyReport = lab.Icmp.HandleIncoming(lab.Pc0, deliveredReplyIp);

        Assert.True(replyReport.IsSuccess);
        Assert.Equal(IcmpType.EchoReply, replyReport.MessageType);
        Assert.Equal(1234, replyReport.Message!.Identifier);
        Assert.Equal(1, replyReport.Message.SequenceNumber);
    }

    [Fact]
    public void FullPing_WithAPrePopulatedArpCache_SendsNoArpRequest()
    {
        var lab = BuildLab();
        lab.Pc0.ArpCache.AddOrUpdateDynamic(Pc1Ip, lab.Pc1.MacAddress!.Value);

        var requestsGenerated = 0;
        lab.Arp.RequestCreated += (_, _) => requestsGenerated++;

        var resolution = lab.Arp.Resolve(lab.Pc0, Pc1Ip);
        Assert.True(resolution.IsResolved);
        Assert.Equal(0, requestsGenerated);

        var echoRequest = lab.Icmp.CreateEchoRequest(1, 1);
        var requestIp = lab.Icmp.Encapsulate(echoRequest, Pc0Ip, Pc1Ip);
        var requestFrame = lab.Ipv4.Encapsulate(requestIp, lab.Pc0.MacAddress!.Value, resolution.HardwareAddress!.Value);
        var toPc1 = lab.Ethernet.Transmit(lab.Network, lab.Pc0, requestFrame);

        Assert.True(toPc1.IsSuccess);
        var deliveredIp = Assert.IsType<IPv4Packet>(DeliveredFrame(toPc1).Payload);
        var report = lab.Icmp.HandleIncoming(lab.Pc1, deliveredIp);
        Assert.True(report.ReplyGenerated);
    }

    [Fact]
    public void EchoRequest_ToAnAddressPc1DoesNotOwn_GetsNoReply()
    {
        var lab = BuildLab();
        var echoRequest = lab.Icmp.CreateEchoRequest(1, 1);
        var requestIp = lab.Icmp.Encapsulate(echoRequest, Pc0Ip, AbsentIp);
        var requestFrame = lab.Ipv4.Encapsulate(requestIp, lab.Pc0.MacAddress!.Value, lab.Pc1.MacAddress!.Value);

        var toPc1 = lab.Ethernet.Transmit(lab.Network, lab.Pc0, requestFrame);
        var deliveredIp = Assert.IsType<IPv4Packet>(DeliveredFrame(toPc1).Payload);
        var report = lab.Icmp.HandleIncoming(lab.Pc1, deliveredIp);

        Assert.True(report.IsSuccess);
        Assert.False(report.LocalInterfaceOwnsDestination);
        Assert.False(report.ReplyGenerated);
    }

    [Fact]
    public void MultipleEchoRequests_EachSequenceNumberIsAnsweredIndependently()
    {
        var lab = BuildLab();
        var pc1Mac = ResolvePc1Mac(lab);

        for (ushort sequence = 1; sequence <= 4; sequence++)
        {
            var echoRequest = lab.Icmp.CreateEchoRequest(100, sequence, RawPayload.FromText($"seq{sequence}"));
            var requestIp = lab.Icmp.Encapsulate(echoRequest, Pc0Ip, Pc1Ip);
            var requestFrame = lab.Ipv4.Encapsulate(requestIp, lab.Pc0.MacAddress!.Value, pc1Mac);

            var toPc1 = lab.Ethernet.Transmit(lab.Network, lab.Pc0, requestFrame);
            var deliveredIp = Assert.IsType<IPv4Packet>(DeliveredFrame(toPc1).Payload);
            var report = lab.Icmp.HandleIncoming(lab.Pc1, deliveredIp);

            Assert.True(report.ReplyGenerated);
            Assert.Equal(100, report.Reply!.Identifier);
            Assert.Equal(sequence, report.Reply.SequenceNumber);
        }
    }

    [Fact]
    public void EchoRequestFrame_IsCarriedByTheEngineWithTheIcmpPayloadIntact()
    {
        var lab = BuildLab();
        var pc1Mac = ResolvePc1Mac(lab);
        var echoRequest = lab.Icmp.CreateEchoRequest(1, 1);
        var requestIp = lab.Icmp.Encapsulate(echoRequest, Pc0Ip, Pc1Ip);
        var requestFrame = lab.Ipv4.Encapsulate(requestIp, lab.Pc0.MacAddress!.Value, pc1Mac);

        var toPc1 = lab.Ethernet.Transmit(lab.Network, lab.Pc0, requestFrame);

        var carriedFrame = DeliveredFrame(toPc1);
        Assert.Equal(EtherType.IPv4, carriedFrame.EtherType);
        var carriedIp = Assert.IsType<IPv4Packet>(carriedFrame.Payload);
        Assert.Equal(ProtocolNumber.Icmp, carriedIp.Protocol);
        var carriedIcmp = Assert.IsType<IcmpMessage>(carriedIp.Payload);
        Assert.True(carriedIcmp.IsEchoRequest);
        Assert.Equal(PacketProcessingOutcome.Delivered, new IcmpProcessor().Process(toPc1.Packet!).Outcome);
    }

    [Fact]
    public void CorruptedEchoRequest_IsDroppedByTheProcessor_WithoutCrashing()
    {
        var lab = BuildLab();
        var pc1Mac = ResolvePc1Mac(lab);
        var echoRequest = lab.Icmp.CreateEchoRequest(1, 1, RawPayload.FromText("Hello"));
        var corrupted = echoRequest.WithChecksum(unchecked((ushort)(echoRequest.Checksum + 1)));
        var requestIp = lab.Icmp.Encapsulate(corrupted, Pc0Ip, Pc1Ip);
        var requestFrame = lab.Ipv4.Encapsulate(requestIp, lab.Pc0.MacAddress!.Value, pc1Mac);

        var toPc1 = lab.Ethernet.Transmit(lab.Network, lab.Pc0, requestFrame);
        var deliveredIp = Assert.IsType<IPv4Packet>(DeliveredFrame(toPc1).Payload);
        var report = lab.Icmp.HandleIncoming(lab.Pc1, deliveredIp);

        Assert.False(report.IsSuccess);
        Assert.Same(IcmpDropReasons.InvalidChecksum, report.DropReason);
    }
}
