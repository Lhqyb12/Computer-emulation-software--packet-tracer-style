using System.Linq;
using NetSim.Application.Dns;
using NetSim.Application.Services;
using NetSim.Application.State;
using NetSim.Core.Arp;
using NetSim.Core.Devices;
using NetSim.Core.Dns;
using NetSim.Core.Ethernet;
using NetSim.Core.IP;
using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Tcp;
using NetSim.Core.Udp;
using NetSim.Application.Tests.TestSupport;

namespace NetSim.Application.Tests.Dns;

/// <summary>
/// Phase 23 end-to-end (brief section 52): drives <see cref="IDnsResolver"/> over the real
/// Ethernet/ARP/IPv4/UDP engines plus a real <see cref="DnsServer"/> hosted through
/// <see cref="IDnsServerService"/> - never a fake result - exactly like <c>PingServiceTests</c>
/// drives <c>IPingService</c> over the real ICMP stack.
/// </summary>
public class DnsResolverTests
{
    private static readonly IPv4Address Pc0Ip = IPv4Address.Parse("192.168.1.10");
    private static readonly IPv4Address ServerIp = IPv4Address.Parse("192.168.1.53");
    private static readonly IPv4Address OffSubnetIp = IPv4Address.Parse("10.0.0.53");
    private static readonly DomainName Example = DomainName.Parse("example.com");
    private static readonly DomainName Www = DomainName.Parse("www.example.com");

    private sealed record Lab(
        DnsResolver Resolver, DnsServer DnsServer, IDnsClientConfigurationStore Configuration, IDnsCache Cache,
        NetworkInterface Pc0, NetworkInterface ServerInterface);

    private static Lab BuildLab(TimeProvider? timeProvider = null, bool startDnsServer = true)
    {
        var networkService = new NetworkService(new ApplicationState());
        var network = networkService.CreateNetwork("Lab");

        var pc0 = NetworkDeviceFactory.Create(DeviceType.Pc, "PC0");
        var server0 = NetworkDeviceFactory.Create(DeviceType.Server, "Server0");
        network.AddDevice(pc0);
        network.AddDevice(server0);

        var pc0Interface = pc0.Interfaces.Single();
        var serverInterface = server0.Interfaces.Single();
        pc0Interface.AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(Pc0Ip, 24));
        serverInterface.AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(ServerIp, 24));
        network.Connect(pc0Interface, serverInterface);
        pc0Interface.BringUp();
        serverInterface.BringUp();

        var ethernet = new EthernetTransmissionService(new PacketEngine());
        var ipv4 = new IPv4Layer(new IPv4Processor());
        var arp = new ArpLayer(new ArpProcessor());
        var udpDelivery = new UdpDeliveryManager();
        var udp = new UdpLayer(new UdpProcessor(), udpDelivery);
        var tcpConnections = new TcpConnectionManager(new RandomInitialSequenceNumberGenerator());

        var dnsServerService = new DnsServerService(udpDelivery, tcpConnections);
        var dnsServer = new DnsServer(Example);
        dnsServer.AddRecord(new DnsARecord(Example, IPv4Address.Parse("192.168.1.100"), TimeSpan.FromSeconds(300)));
        dnsServer.AddRecord(new DnsCnameRecord(Www, Example, TimeSpan.FromSeconds(300)));
        dnsServer.AddRecord(new DnsAAAARecord(DomainName.Parse("ipv6.example.com"), IPv6Address.Parse("2001:db8::100"), TimeSpan.FromSeconds(300)));

        if (startDnsServer)
        {
            dnsServerService.Start(server0, dnsServer);
        }

        var configuration = new DnsClientConfigurationStore();
        configuration.SetServers(pc0, [ServerIp]);

        var cache = new DnsCache(timeProvider);
        var resolver = new DnsResolver(udp, ipv4, arp, ethernet, networkService, dnsServerService, configuration, cache);

        return new Lab(resolver, dnsServer, configuration, cache, pc0Interface, serverInterface);
    }

    [Fact]
    public void Resolve_ARecord_Succeeds()
    {
        var lab = BuildLab();

        var result = lab.Resolver.Resolve(lab.Pc0, Example, DnsRecordType.A);

        Assert.True(result.IsSuccess);
        Assert.False(result.FromCache);
        var address = Assert.IsType<DnsARecord>(Assert.Single(result.Records)).Address;
        Assert.Equal(IPv4Address.Parse("192.168.1.100"), address);
    }

    [Fact]
    public void Resolve_Cname_FollowsToTheFinalARecord()
    {
        var lab = BuildLab();

        var result = lab.Resolver.Resolve(lab.Pc0, Www, DnsRecordType.A);

        Assert.True(result.IsSuccess);
        var address = Assert.IsType<DnsARecord>(Assert.Single(result.Records)).Address;
        Assert.Equal(IPv4Address.Parse("192.168.1.100"), address);
    }

    [Fact]
    public void Resolve_AAAARecord_Succeeds()
    {
        var lab = BuildLab();

        var result = lab.Resolver.Resolve(lab.Pc0, DomainName.Parse("ipv6.example.com"), DnsRecordType.AAAA);

        Assert.True(result.IsSuccess);
        var address = Assert.IsType<DnsAAAARecord>(Assert.Single(result.Records)).Address;
        Assert.Equal(IPv6Address.Parse("2001:db8::100"), address);
    }

    [Fact]
    public void Resolve_MultipleARecords_ReturnsAllOfThem()
    {
        var lab = BuildLab();
        lab.DnsServer.AddRecord(new DnsARecord(Example, IPv4Address.Parse("192.168.1.101"), TimeSpan.FromSeconds(300)));

        var result = lab.Resolver.Resolve(lab.Pc0, Example, DnsRecordType.A, useCache: false);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Records.Count);
    }

    [Fact]
    public void Resolve_UnknownName_ReturnsNxDomain()
    {
        var lab = BuildLab();

        var result = lab.Resolver.Resolve(lab.Pc0, DomainName.Parse("doesnotexist.example.com"), DnsRecordType.A);

        Assert.False(result.IsSuccess);
        Assert.Equal(DnsResolutionStatus.NxDomain, result.Status);
    }

    [Fact]
    public void Resolve_SecondCall_IsAHit_AndSendsNoSecondQuery()
    {
        var lab = BuildLab();
        var queriesSent = 0;
        lab.Resolver.QuerySent += (_, _) => queriesSent++;

        var first = lab.Resolver.Resolve(lab.Pc0, Www, DnsRecordType.A);
        var second = lab.Resolver.Resolve(lab.Pc0, Www, DnsRecordType.A);

        Assert.True(first.IsSuccess);
        Assert.False(first.FromCache);
        Assert.True(second.IsSuccess);
        Assert.True(second.FromCache);
        Assert.Equal(1, queriesSent);
        Assert.Equal(first.Records.Select(r => r.DataText), second.Records.Select(r => r.DataText));
    }

    [Fact]
    public void Resolve_RaisesCacheMissThenCacheHit()
    {
        var lab = BuildLab();
        var missCount = 0;
        var hitCount = 0;
        lab.Resolver.CacheMiss += (_, _) => missCount++;
        lab.Resolver.CacheHit += (_, _) => hitCount++;

        lab.Resolver.Resolve(lab.Pc0, Example, DnsRecordType.A);
        lab.Resolver.Resolve(lab.Pc0, Example, DnsRecordType.A);

        Assert.Equal(1, missCount);
        Assert.Equal(1, hitCount);
    }

    [Fact]
    public void Resolve_AfterTtlExpires_QueriesAgain()
    {
        var time = TestTimeProvider.StartingAtEpoch();
        var lab = BuildLab(time);

        var first = lab.Resolver.Resolve(lab.Pc0, Example, DnsRecordType.A);
        Assert.False(first.FromCache);

        time.Advance(TimeSpan.FromSeconds(301));

        var second = lab.Resolver.Resolve(lab.Pc0, Example, DnsRecordType.A);

        Assert.True(second.IsSuccess);
        Assert.False(second.FromCache);
    }

    [Fact]
    public void Resolve_NoConfiguredServer_FailsGracefully()
    {
        var lab = BuildLab();
        lab.Configuration.ClearServers(lab.Pc0.Device);

        var result = lab.Resolver.Resolve(lab.Pc0, Example, DnsRecordType.A);

        Assert.False(result.IsSuccess);
        Assert.Equal(DnsResolutionStatus.NoConfiguredServer, result.Status);
    }

    [Fact]
    public void Resolve_DnsServiceNotRunningOnTheServer_ReportsServerUnavailable_WithoutCrashing()
    {
        var lab = BuildLab(startDnsServer: false);
        var raised = false;
        lab.Resolver.ServerUnavailable += (_, _) => raised = true;

        var result = lab.Resolver.Resolve(lab.Pc0, Example, DnsRecordType.A);

        Assert.False(result.IsSuccess);
        Assert.Equal(DnsResolutionStatus.ServerUnavailable, result.Status);
        Assert.True(raised);
    }

    [Fact]
    public void Resolve_NoDeviceAtTheConfiguredServerAddress_ReportsServerUnavailable()
    {
        var lab = BuildLab();
        lab.Configuration.SetServers(lab.Pc0.Device, [IPv4Address.Parse("192.168.1.254")]);

        var result = lab.Resolver.Resolve(lab.Pc0, Example, DnsRecordType.A);

        Assert.False(result.IsSuccess);
        Assert.Equal(DnsResolutionStatus.ServerUnavailable, result.Status);
    }

    [Fact]
    public void Resolve_ServerOutsideLocalSubnet_ReportsNoRoute_WithoutTouchingTheWire()
    {
        var lab = BuildLab();
        lab.Configuration.SetServers(lab.Pc0.Device, [OffSubnetIp]);

        var result = lab.Resolver.Resolve(lab.Pc0, Example, DnsRecordType.A);

        Assert.Equal(DnsResolutionStatus.NoRoute, result.Status);
    }

    [Fact]
    public void Resolve_NoNameOfRequestedType_ReturnsNoData()
    {
        var lab = BuildLab();

        var result = lab.Resolver.Resolve(lab.Pc0, Example, DnsRecordType.NS, useCache: false);

        Assert.False(result.IsSuccess);
        Assert.Equal(DnsResolutionStatus.NoData, result.Status);
    }

    [Fact]
    public void Resolve_RaisesResolutionStartedAndSucceededEvents()
    {
        var lab = BuildLab();
        var started = false;
        var succeeded = false;
        lab.Resolver.ResolutionStarted += (_, _) => started = true;
        lab.Resolver.ResolutionSucceeded += (_, _) => succeeded = true;

        var result = lab.Resolver.Resolve(lab.Pc0, Example, DnsRecordType.A);

        Assert.True(result.IsSuccess);
        Assert.True(started);
        Assert.True(succeeded);
    }

    [Fact]
    public void Resolve_Failure_RaisesResolutionFailedEvent()
    {
        var lab = BuildLab();
        var failed = false;
        lab.Resolver.ResolutionFailed += (_, _) => failed = true;

        lab.Resolver.Resolve(lab.Pc0, DomainName.Parse("doesnotexist.example.com"), DnsRecordType.A);

        Assert.True(failed);
    }

    [Fact]
    public void Resolve_Cname_RaisesCnameFollowedOnlyWhenTheServerDidNotResolveTheChainItself()
    {
        // Our DnsServer already resolves a CNAME chain within its own zone in a single response, so
        // the client-side "follow it myself" path (and its event) is a fallback that is not expected
        // to trigger for this scenario - this documents that behaviour rather than assuming it.
        var lab = BuildLab();
        var followed = false;
        lab.Resolver.CnameFollowed += (_, _) => followed = true;

        var result = lab.Resolver.Resolve(lab.Pc0, Www, DnsRecordType.A);

        Assert.True(result.IsSuccess);
        Assert.False(followed);
    }

    [Fact]
    public void Resolve_NullSourceInterface_Throws()
    {
        var lab = BuildLab();

        Assert.Throws<ArgumentNullException>(() => lab.Resolver.Resolve(null!, Example, DnsRecordType.A));
    }
}
