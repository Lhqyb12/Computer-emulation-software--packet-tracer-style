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

namespace NetSim.Application.Tests.Diagnostics;

/// <summary>
/// Phase 21 end-to-end: <see cref="IPingService"/> is the "PingView -&gt; PingViewModel -&gt;
/// IPingService -&gt; ICMP Engine" seam (brief section 37) - these tests drive it exactly as the UI
/// would, over the real Core engines (ARP / ICMP / IPv4 / Ethernet), never a fake result.
/// </summary>
public class PingServiceTests
{
    private static readonly IPv4Address Pc0Ip = IPv4Address.Parse("192.168.1.10");
    private static readonly IPv4Address Pc1Ip = IPv4Address.Parse("192.168.1.20");
    private static readonly IPv4Address AbsentIp = IPv4Address.Parse("192.168.1.30");
    private static readonly IPv4Address OffSubnetIp = IPv4Address.Parse("10.0.0.5");

    private sealed record Lab(
        PingService Service, IArpLayer Arp, NetSim.Core.Topology.Network Network, NetworkInterface Pc0, NetworkInterface Pc1);

    private static Lab BuildLab()
    {
        var networkService = new NetworkService(new ApplicationState());
        var network = networkService.CreateNetwork("Lab");

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

        var ethernet = new EthernetTransmissionService(new PacketEngine());
        var ipv4 = new IPv4Layer(new IPv4Processor());
        var arp = new ArpLayer(new ArpProcessor());
        var icmp = new IcmpLayer(new IcmpProcessor());
        var service = new PingService(icmp, ipv4, arp, ethernet, networkService);

        return new Lab(service, arp, network, g0, g1);
    }

    [Fact]
    public void Ping_ArpCacheMiss_SucceedsForEveryRequest()
    {
        var lab = BuildLab();

        var result = lab.Service.Ping(lab.Pc0, Pc1Ip, count: 4);

        Assert.Equal(4, result.PacketsSent);
        Assert.Equal(4, result.PacketsReceived);
        Assert.Equal(0, result.PacketsLost);
        Assert.Equal(0, result.PacketLossPercentage);
        Assert.True(result.IsFullSuccess);
        Assert.All(result.Replies, r => Assert.True(r.IsSuccess));
        Assert.Equal([1, 2, 3, 4], result.Replies.Select(r => r.SequenceNumber));
        Assert.All(result.Replies, r => Assert.Equal(Pc1Ip, r.RespondingAddress));
        Assert.NotNull(result.AverageRoundTripTime);
    }

    [Fact]
    public void Ping_ArpCacheHit_SendsNoArpRequestFromTheSource_AndSucceeds()
    {
        var lab = BuildLab();
        lab.Pc0.ArpCache.AddOrUpdateDynamic(Pc1Ip, lab.Pc1.MacAddress!.Value);

        // The forward resolution (PC0 -> PC1) is a cache hit and must generate no request; PC1 may
        // still need to ARP for PC0 on the return trip if it has not learned it yet (a separate,
        // expected exchange - this test only asserts the cache hit itself is honoured).
        var requestsFromSource = 0;
        lab.Arp.RequestCreated += (_, e) =>
        {
            if (ReferenceEquals(e.Interface, lab.Pc0))
            {
                requestsFromSource++;
            }
        };

        var result = lab.Service.Ping(lab.Pc0, Pc1Ip, count: 1);

        Assert.True(result.IsFullSuccess);
        Assert.Equal(0, requestsFromSource);
    }

    [Fact]
    public void Ping_DestinationNobodyOwns_TimesOut()
    {
        var lab = BuildLab();

        var result = lab.Service.Ping(lab.Pc0, AbsentIp, count: 1);

        Assert.False(result.IsFullSuccess);
        Assert.Equal(PingReplyStatus.TimedOut, result.Replies.Single().Status);
        Assert.NotNull(result.Replies.Single().ErrorMessage);
    }

    [Fact]
    public void Ping_OffSubnetDestination_ReportsNoRoute_WithoutTouchingTheWire()
    {
        var lab = BuildLab();

        var result = lab.Service.Ping(lab.Pc0, OffSubnetIp, count: 1);

        Assert.Equal(PingReplyStatus.NoRoute, result.Replies.Single().Status);
    }

    [Fact]
    public void Ping_NoIPv4OnSourceInterface_ReportsNoSourceAddress()
    {
        var lab = BuildLab();
        var pc2 = NetworkDeviceFactory.Create(DeviceType.Pc, "PC2");
        lab.Network.AddDevice(pc2);
        var unconfigured = pc2.Interfaces.Single();

        var result = lab.Service.Ping(unconfigured, Pc1Ip, count: 1);

        Assert.Equal(PingReplyStatus.NoSourceAddress, result.Replies.Single().Status);
    }

    [Fact]
    public void Ping_InvalidDestination_IsRejected()
    {
        var lab = BuildLab();

        var result = lab.Service.Ping(lab.Pc0, IPv4Address.Parse("255.255.255.255"), count: 1);

        Assert.Equal(PingReplyStatus.InvalidDestination, result.Replies.Single().Status);
    }

    [Fact]
    public void Ping_MultipleSequences_ProduceIncreasingSequenceNumbers_WithTheSameIdentifier()
    {
        var lab = BuildLab();

        var result = lab.Service.Ping(lab.Pc0, Pc1Ip, count: 4);

        var identifiers = result.Replies.Select(r => r.EchoRequest!.Identifier).Distinct().ToList();
        Assert.Single(identifiers);
        Assert.Equal([1, 2, 3, 4], result.Replies.Select(r => r.EchoRequest!.SequenceNumber));
    }

    [Fact]
    public void Ping_Count_MustBeAtLeastOne()
    {
        var lab = BuildLab();

        Assert.Throws<ArgumentOutOfRangeException>(() => lab.Service.Ping(lab.Pc0, Pc1Ip, count: 0));
    }

    [Fact]
    public void Ping_NullSourceInterface_Throws()
    {
        var lab = BuildLab();

        Assert.Throws<ArgumentNullException>(() => lab.Service.Ping(null!, Pc1Ip));
    }
}
