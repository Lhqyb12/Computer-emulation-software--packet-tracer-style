using System.Collections.Generic;
using System.Linq;
using NetSim.Application.Dhcp;
using NetSim.Application.Dns;
using NetSim.Application.Services;
using NetSim.Application.State;
using NetSim.Application.Tests.TestSupport;
using NetSim.Core.Arp;
using NetSim.Core.Devices;
using NetSim.Core.Dhcp;
using NetSim.Core.Dns;
using NetSim.Core.Ethernet;
using NetSim.Core.IP;
using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Tcp;
using NetSim.Core.Topology;
using NetSim.Core.Udp;

namespace NetSim.Application.Tests.Dhcp;

/// <summary>
/// Phase 24 end-to-end (brief sections 55-64): drives <see cref="IDhcpClient"/> over the real
/// Ethernet/IPv4/UDP engines plus a real <see cref="DhcpServer"/> hosted through
/// <see cref="IDhcpServerService"/> - never a fake result - exactly like <c>DnsResolverTests</c>
/// drives the DNS stack.
/// </summary>
public class DhcpClientTests
{
    private static readonly IPv4Address ServerIp = IPv4Address.Parse("192.168.1.1");
    private static readonly IPv4Address DnsIp = IPv4Address.Parse("192.168.1.53");

    private sealed class Lab
    {
        public required DhcpClient Client { get; init; }
        public required DhcpServer Server { get; init; }
        public required IDhcpServerService ServerService { get; init; }
        public required IDhcpClientStateStore StateStore { get; init; }
        public required IDnsClientConfigurationStore DnsConfiguration { get; init; }
        public required NetworkInterface Pc0 { get; init; }
        public required NetworkDevice Pc0Device { get; init; }
        public required NetworkDevice ServerDevice { get; init; }
        public required Network Network { get; init; }
        public required TestTimeProvider Time { get; init; }
        public required SequentialDhcpTransactionIdSource Xid { get; init; }
        public required EthernetTransmissionService Ethernet { get; init; }
        public required IPv4Layer IPv4 { get; init; }
        public required UdpLayer Udp { get; init; }
        public required UdpDeliveryManager UdpDelivery { get; init; }
        public required NetworkService NetworkService { get; init; }
    }

    private static Lab BuildLab(
        string poolStart = "192.168.1.100", string poolEnd = "192.168.1.200",
        IPv4Address? dnsServer = null, TimeSpan? leaseDuration = null, bool startServer = true)
    {
        var networkService = new NetworkService(new ApplicationState());
        var network = networkService.CreateNetwork("Lab");

        var pc0 = NetworkDeviceFactory.Create(DeviceType.Pc, "PC0");
        var server0 = NetworkDeviceFactory.Create(DeviceType.Server, "Server0");
        network.AddDevice(pc0);
        network.AddDevice(server0);

        var pc0Interface = pc0.Interfaces.Single();
        var serverInterface = server0.Interfaces.Single();
        serverInterface.AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(ServerIp, 24));
        network.Connect(pc0Interface, serverInterface);
        pc0Interface.BringUp();
        serverInterface.BringUp();

        var ethernet = new EthernetTransmissionService(new PacketEngine());
        var ipv4 = new IPv4Layer(new IPv4Processor());
        var udpDelivery = new UdpDeliveryManager();
        var udp = new UdpLayer(new UdpProcessor(), udpDelivery);

        var time = TestTimeProvider.StartingAtEpoch();
        var xid = new SequentialDhcpTransactionIdSource();
        var stateStore = new DhcpClientStateStore();
        var dnsConfiguration = new DnsClientConfigurationStore();
        var serverService = new DhcpServerService(udpDelivery);

        var config = new DhcpServerConfiguration(
            ServerIp, 24, IPv4Address.Parse(poolStart), IPv4Address.Parse(poolEnd),
            defaultGateway: ServerIp, dnsServer: dnsServer ?? DnsIp, leaseDuration: leaseDuration ?? TimeSpan.FromSeconds(3600));
        var dhcpServer = new DhcpServer(config, time);
        if (startServer)
        {
            serverService.Start(server0, dhcpServer);
        }

        var client = new DhcpClient(udp, ipv4, ethernet, networkService, serverService, dnsConfiguration, stateStore, xid, time);

        return new Lab
        {
            Client = client, Server = dhcpServer, ServerService = serverService, StateStore = stateStore,
            DnsConfiguration = dnsConfiguration, Pc0 = pc0Interface, Pc0Device = pc0, ServerDevice = server0,
            Network = network, Time = time, Xid = xid, Ethernet = ethernet, IPv4 = ipv4, Udp = udp,
            UdpDelivery = udpDelivery, NetworkService = networkService,
        };
    }

    [Fact]
    public void Acquire_RunsTheFullDoraExchange_AndBindsTheInterface()
    {
        var lab = BuildLab();
        var events = new List<string>();
        lab.Client.DiscoverCreated += (_, _) => events.Add("discover-created");
        lab.Client.DiscoverSent += (_, _) => events.Add("discover-sent");
        lab.Client.OfferReceived += (_, _) => events.Add("offer");
        lab.Client.RequestCreated += (_, _) => events.Add("request-created");
        lab.Client.RequestSent += (_, _) => events.Add("request-sent");
        lab.Client.AckReceived += (_, _) => events.Add("ack");
        lab.Client.ConfigurationApplied += (_, _) => events.Add("applied");
        lab.Client.LeaseBound += (_, _) => events.Add("bound");

        var result = lab.Client.Acquire(lab.Pc0);

        Assert.True(result.IsSuccess);
        Assert.Equal(DhcpAcquireStatus.Bound, result.Status);
        Assert.Equal(IPv4Address.Parse("192.168.1.100"), result.Address);
        Assert.Equal(SubnetMask.Parse("255.255.255.0"), result.SubnetMask);
        Assert.Equal(ServerIp, result.Gateway);
        Assert.Equal(DnsIp, result.DnsServer);
        Assert.Equal(TimeSpan.FromSeconds(3600), result.LeaseDuration);
        Assert.Equal(ServerIp, result.Server);

        Assert.Equal("192.168.1.100/24", lab.Pc0.PrimaryIPv4Configuration!.Cidr);
        Assert.Equal(DhcpClientState.Bound, lab.StateStore.GetOrCreate(lab.Pc0).State);
        Assert.Equal(new[] { "discover-created", "discover-sent", "offer", "request-created", "request-sent", "ack", "applied", "bound" }, events);
    }

    [Fact]
    public void Acquire_TransactionIds_MatchBetweenDiscoverOfferAndRequestAck()
    {
        var lab = BuildLab();
        var expectedXid = lab.Xid.Peek;

        var result = lab.Client.Acquire(lab.Pc0);

        Assert.Equal(expectedXid, result.TransactionId);
        Assert.Equal(expectedXid, lab.StateStore.GetOrCreate(lab.Pc0).LastTransactionId);
    }

    [Fact]
    public void Acquire_UsesUdpPorts68And67_AndNoRealNetworking()
    {
        // The whole exchange runs through the simulated UdpDeliveryManager/UdpLayer; a server
        // binding on port 67 is what DhcpServerService created, and the client sends from 68.
        var lab = BuildLab();
        Assert.True(lab.UdpDelivery.IsBound(lab.ServerDevice, DhcpProtocol.ServerPort));
        Assert.Equal(67, DhcpProtocol.ServerPort.Value);
        Assert.Equal(68, DhcpProtocol.ClientPort.Value);

        var result = lab.Client.Acquire(lab.Pc0);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Acquire_WithNoServerRunning_ReportsNoOffer_WithoutCrashing()
    {
        var lab = BuildLab(startServer: false);
        var serverUnavailable = false;
        lab.Client.ServerUnavailable += (_, _) => serverUnavailable = true;

        var result = lab.Client.Acquire(lab.Pc0);

        Assert.False(result.IsSuccess);
        Assert.Equal(DhcpAcquireStatus.NoOffer, result.Status);
        Assert.True(serverUnavailable);
        Assert.False(lab.Pc0.HasIPv4Configuration);
    }

    [Fact]
    public void Acquire_OnADownInterface_ReportsInterfaceUnusable()
    {
        var lab = BuildLab();
        lab.Pc0.BringDown();

        var result = lab.Client.Acquire(lab.Pc0);

        Assert.Equal(DhcpAcquireStatus.InterfaceUnusable, result.Status);
    }

    [Fact]
    public void Acquire_SuppliesTheDnsServer_WhichThePhase23ResolverThenUses()
    {
        // DHCP hands out the server's own address as the DNS server, and a DNS service runs there.
        var lab = BuildLab(dnsServer: ServerIp);

        var dnsServer = new DnsServer(DomainName.Parse("example.com"));
        dnsServer.AddRecord(new DnsARecord(DomainName.Parse("example.com"), IPv4Address.Parse("93.184.216.34"), TimeSpan.FromSeconds(300)));
        var dnsServerService = new DnsServerService(lab.UdpDelivery, new TcpConnectionManager(new RandomInitialSequenceNumberGenerator()));
        dnsServerService.Start(lab.ServerDevice, dnsServer);

        var acquire = lab.Client.Acquire(lab.Pc0);
        Assert.True(acquire.IsSuccess);
        Assert.Equal([ServerIp], lab.DnsConfiguration.GetServers(lab.Pc0Device));

        // Resolve WITHOUT manually configuring DNS - it must come from DHCP.
        var resolver = new DnsResolver(
            lab.Udp, lab.IPv4, new ArpLayer(new ArpProcessor()), lab.Ethernet, lab.NetworkService,
            dnsServerService, lab.DnsConfiguration, new DnsCache(lab.Time));

        var resolution = resolver.Resolve(lab.Pc0, DomainName.Parse("example.com"), DnsRecordType.A);

        Assert.True(resolution.IsSuccess);
        Assert.Equal(IPv4Address.Parse("93.184.216.34"), ((DnsARecord)resolution.Records.Single()).Address);
    }

    [Fact]
    public void Renew_ExtendsTheLease_AndKeepsTheClientBound()
    {
        var lab = BuildLab();
        var bound = lab.Client.Acquire(lab.Pc0);
        var firstExpiry = lab.StateStore.GetOrCreate(lab.Pc0).Configuration!.LeaseExpiration;
        var renewedRaised = false;
        lab.Client.LeaseRenewed += (_, _) => renewedRaised = true;

        lab.Time.Advance(TimeSpan.FromSeconds(1800));
        var renew = lab.Client.Renew(lab.Pc0);

        Assert.Equal(DhcpAcquireStatus.Renewed, renew.Status);
        Assert.Equal(bound.Address, renew.Address);
        Assert.True(renew.Configuration!.LeaseExpiration > firstExpiry);
        Assert.Equal(DhcpClientState.Bound, lab.StateStore.GetOrCreate(lab.Pc0).State);
        Assert.True(renewedRaised);
    }

    [Fact]
    public void Renew_WhenNotBound_ReportsNotBound()
    {
        var lab = BuildLab();

        Assert.Equal(DhcpAcquireStatus.NotBound, lab.Client.Renew(lab.Pc0).Status);
    }

    [Fact]
    public void LeaseExpiration_WithdrawsTheAddress_AndTheClientCanAcquireAgain()
    {
        var lab = BuildLab(leaseDuration: TimeSpan.FromSeconds(60));
        lab.Client.Acquire(lab.Pc0);
        var expiredRaised = false;
        lab.Client.LeaseExpired += (_, _) => expiredRaised = true;

        lab.Time.Advance(TimeSpan.FromSeconds(61));

        Assert.True(lab.Client.IsLeaseExpired(lab.Pc0));
        Assert.True(lab.Client.AbandonExpiredLease(lab.Pc0));
        Assert.True(expiredRaised);
        Assert.False(lab.Pc0.HasIPv4Configuration);
        Assert.Equal(DhcpClientState.Expired, lab.StateStore.GetOrCreate(lab.Pc0).State);

        // Server frees the address on its own lazy prune.
        var expiredOnServer = lab.Server.PruneExpiredLeases();
        Assert.Single(expiredOnServer);

        var again = lab.Client.Acquire(lab.Pc0);
        Assert.True(again.IsSuccess);
        Assert.Equal(IPv4Address.Parse("192.168.1.100"), again.Address);
    }

    [Fact]
    public void Release_SendsRelease_ClearsConfiguration_AndFreesTheAddress()
    {
        var lab = BuildLab();
        lab.Client.Acquire(lab.Pc0);
        var releasedRaised = false;
        lab.Client.LeaseReleased += (_, _) => releasedRaised = true;

        var release = lab.Client.Release(lab.Pc0);

        Assert.True(release.IsReleased);
        Assert.Equal(IPv4Address.Parse("192.168.1.100"), release.ReleasedAddress);
        Assert.False(lab.Pc0.HasIPv4Configuration);
        Assert.Empty(lab.DnsConfiguration.GetServers(lab.Pc0Device));
        Assert.Empty(lab.Server.Leases.ActiveLeases);
        Assert.True(releasedRaised);
        Assert.Equal(DhcpClientState.Init, lab.StateStore.GetOrCreate(lab.Pc0).State);
    }

    [Fact]
    public void Release_WhenNothingIsBound_IsANoOp()
    {
        var lab = BuildLab();

        Assert.False(lab.Client.Release(lab.Pc0).IsReleased);
    }

    [Fact]
    public void MultipleClients_OnTheSameServer_GetDistinctLeases()
    {
        var lab = BuildLab();

        // Two more PCs, each on its own server interface (no switch exists yet - Phase 25).
        var results = new List<DhcpAcquireResult> { lab.Client.Acquire(lab.Pc0) };
        for (var i = 1; i <= 2; i++)
        {
            var pc = NetworkDeviceFactory.Create(DeviceType.Pc, $"PC{i}");
            lab.Network.AddDevice(pc);
            var serverExtra = lab.ServerDevice.AddInterface($"Ethernet{i}", lab.ServerDevice.Interfaces.First().InterfaceType);
            lab.Network.Connect(pc.Interfaces.Single(), serverExtra);
            pc.Interfaces.Single().BringUp();
            serverExtra.BringUp();
            results.Add(lab.Client.Acquire(pc.Interfaces.Single()));
        }

        Assert.All(results, r => Assert.True(r.IsSuccess));
        Assert.Equal(3, results.Select(r => r.Address).Distinct().Count());
        Assert.Equal(3, lab.Server.Leases.ActiveLeases.Count);
    }

    [Fact]
    public void PoolExhaustion_LeavesTheThirdClientWithoutAnAddress()
    {
        var lab = BuildLab(poolStart: "192.168.1.100", poolEnd: "192.168.1.101"); // two addresses
        var poolExhaustedRaised = false;
        lab.Server.PoolExhausted += (_, _) => poolExhaustedRaised = true;

        var first = lab.Client.Acquire(lab.Pc0);

        var pc1 = NetworkDeviceFactory.Create(DeviceType.Pc, "PC1");
        lab.Network.AddDevice(pc1);
        var s1 = lab.ServerDevice.AddInterface("Ethernet1", lab.ServerDevice.Interfaces.First().InterfaceType);
        lab.Network.Connect(pc1.Interfaces.Single(), s1);
        pc1.Interfaces.Single().BringUp();
        s1.BringUp();
        var second = lab.Client.Acquire(pc1.Interfaces.Single());

        var pc2 = NetworkDeviceFactory.Create(DeviceType.Pc, "PC2");
        lab.Network.AddDevice(pc2);
        var s2 = lab.ServerDevice.AddInterface("Ethernet2", lab.ServerDevice.Interfaces.First().InterfaceType);
        lab.Network.Connect(pc2.Interfaces.Single(), s2);
        pc2.Interfaces.Single().BringUp();
        s2.BringUp();
        var third = lab.Client.Acquire(pc2.Interfaces.Single());

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.False(third.IsSuccess);
        Assert.Equal(DhcpAcquireStatus.NoOffer, third.Status);
        Assert.True(poolExhaustedRaised);
        Assert.False(pc2.Interfaces.Single().HasIPv4Configuration);
    }

    [Fact]
    public void MultipleServers_BothOffer_ClientSelectsTheFirst_OtherServerDoesNotFinaliseItsOffer()
    {
        var lab = BuildLab();

        // A second server on a second PC0 interface. Same subnet + pool -> both offer .100.
        var serverB = NetworkDeviceFactory.Create(DeviceType.Server, "ServerB");
        lab.Network.AddDevice(serverB);
        var serverBInterface = serverB.Interfaces.Single();
        serverBInterface.AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("192.168.1.2"), 24));
        var pc0SecondInterface = lab.Pc0Device.AddInterface("Ethernet1", lab.Pc0.InterfaceType);
        lab.Network.Connect(pc0SecondInterface, serverBInterface);
        pc0SecondInterface.BringUp();
        serverBInterface.BringUp();

        var dhcpServerB = new DhcpServer(new DhcpServerConfiguration(
            IPv4Address.Parse("192.168.1.2"), 24, IPv4Address.Parse("192.168.1.100"), IPv4Address.Parse("192.168.1.200"),
            defaultGateway: IPv4Address.Parse("192.168.1.2"), dnsServer: DnsIp), lab.Time);
        lab.ServerService.Start(serverB, dhcpServerB);

        var result = lab.Client.Acquire(lab.Pc0);

        Assert.True(result.IsSuccess);
        // Exactly one server ends up with an active lease; the other's offer was cancelled.
        var totalActive = lab.Server.Leases.ActiveLeases.Count + dhcpServerB.Leases.ActiveLeases.Count;
        Assert.Equal(1, totalActive);
        Assert.DoesNotContain(dhcpServerB.Leases.Leases.Concat(lab.Server.Leases.Leases), l => l.State == DhcpLeaseState.Offered);
    }

    [Fact]
    public void InvalidRequest_ForAnAddressOutsideThePool_IsNakedAndAppliesNothing()
    {
        // Drive a REQUEST directly at the server for an out-of-pool address via a fresh client whose
        // "offer" we fake by asking for .250. The client always requests what it was offered, so we
        // exercise this at the server boundary and assert the whole client stays unconfigured.
        var lab = BuildLab(poolStart: "192.168.1.100", poolEnd: "192.168.1.120");

        var nak = lab.Server.HandleMessage(DhcpMessage.CreateRequest(
            0x1, lab.Pc0.MacAddress!.Value,
            new DhcpOptions.Builder { RequestedIpAddress = IPv4Address.Parse("192.168.1.250"), ServerIdentifier = ServerIp }.Build()));

        Assert.Equal(DhcpMessageType.Nak, nak!.MessageType);
        Assert.Empty(lab.Server.Leases.ActiveLeases);
        Assert.False(lab.Pc0.HasIPv4Configuration);
    }

    [Fact]
    public void StaticallyConfiguredInterface_IsNotDisturbed_WhenDhcpIsNeverStarted()
    {
        var lab = BuildLab();
        lab.Pc0.AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("192.168.1.250"), 24));

        Assert.False(lab.StateStore.IsDhcpManaged(lab.Pc0));
        Assert.Equal("192.168.1.250/24", lab.Pc0.PrimaryIPv4Configuration!.Cidr);
    }
}
