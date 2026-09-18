using Microsoft.Extensions.DependencyInjection;
using NetSim.Application.Canvas;
using NetSim.Application.DependencyInjection;
using NetSim.Application.Persistence;
using NetSim.Application.Services;
using NetSim.Application.State;
using NetSim.Application.Tests.TestSupport;

namespace NetSim.Application.Tests.DependencyInjection;

public class ApplicationServiceCollectionExtensionsTests
{
    // IProjectService (registered by AddApplicationServices) depends on IProjectRepository,
    // whose real implementation lives in NetworkSimulator.Infrastructure - out of reach for this
    // test project by design (Application must not reference Infrastructure). Resolving
    // IProjectService here needs a repository registered, so every test that builds a provider
    // registers this in-memory fake first, exactly as NetworkSimulator.App's CompositionRoot
    // registers the real one alongside AddApplicationServices.
    private static ServiceCollection CreateServicesWithProjectRepository()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IProjectRepository, InMemoryProjectRepository>();
        return services;
    }

    [Fact]
    public void AddApplicationServices_ReturnsSameServiceCollection_ForChaining()
    {
        var services = CreateServicesWithProjectRepository();

        var result = services.AddApplicationServices();

        Assert.Same(services, result);
    }

    [Fact]
    public void AddApplicationServices_BuildsAValidServiceProvider()
    {
        var services = CreateServicesWithProjectRepository();
        services.AddApplicationServices();

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider);
    }

    [Theory]
    [InlineData(typeof(IApplicationState))]
    [InlineData(typeof(ISelectionState))]
    [InlineData(typeof(ICanvasState))]
    [InlineData(typeof(NetSim.Core.Topology.ITopologyQueryService))]
    [InlineData(typeof(NetSim.Core.Packets.IPacketEngine))]
    [InlineData(typeof(NetSim.Core.Ethernet.EthernetProcessor))]
    [InlineData(typeof(NetSim.Core.Ethernet.IEthernetTransmissionService))]
    [InlineData(typeof(NetSim.Core.Switching.ISimulationClock))]
    [InlineData(typeof(NetSim.Core.Switching.ISwitchingEngine))]
    [InlineData(typeof(NetSim.Core.Switching.ISwitchedSegmentService))]
    [InlineData(typeof(NetSim.Core.IP.IPv4Processor))]
    [InlineData(typeof(NetSim.Core.IP.IIPv4Layer))]
    [InlineData(typeof(NetSim.Core.IP.IPv6Processor))]
    [InlineData(typeof(NetSim.Core.IP.IIPv6Layer))]
    [InlineData(typeof(NetSim.Core.Arp.ArpProcessor))]
    [InlineData(typeof(NetSim.Core.Arp.IArpLayer))]
    [InlineData(typeof(NetSim.Core.Icmp.IcmpProcessor))]
    [InlineData(typeof(NetSim.Core.Icmp.IIcmpLayer))]
    [InlineData(typeof(NetSim.Core.Udp.UdpProcessor))]
    [InlineData(typeof(NetSim.Core.Udp.IUdpDeliveryManager))]
    [InlineData(typeof(NetSim.Core.Udp.IUdpLayer))]
    [InlineData(typeof(NetSim.Core.Tcp.IInitialSequenceNumberGenerator))]
    [InlineData(typeof(NetSim.Core.Tcp.TcpProcessor))]
    [InlineData(typeof(NetSim.Core.Tcp.ITcpConnectionManager))]
    [InlineData(typeof(NetSim.Core.Tcp.ITcpLayer))]
    [InlineData(typeof(INetworkService))]
    [InlineData(typeof(IDeviceService))]
    [InlineData(typeof(IConnectionService))]
    [InlineData(typeof(IProjectService))]
    [InlineData(typeof(NetSim.Application.Diagnostics.IPingService))]
    [InlineData(typeof(NetSim.Application.Routing.IStaticRouteService))]
    [InlineData(typeof(NetSim.Core.Dns.IDnsCache))]
    [InlineData(typeof(NetSim.Core.Dns.IDnsClientConfigurationStore))]
    [InlineData(typeof(NetSim.Application.Dns.IDnsServerService))]
    [InlineData(typeof(NetSim.Application.Dns.IDnsResolver))]
    [InlineData(typeof(NetSim.Core.Dhcp.IDhcpTransactionIdSource))]
    [InlineData(typeof(NetSim.Core.Dhcp.IDhcpClientStateStore))]
    [InlineData(typeof(NetSim.Application.Dhcp.IDhcpServerService))]
    [InlineData(typeof(NetSim.Application.Dhcp.IDhcpClient))]
    public void AddApplicationServices_ResolvesEachRegisteredService(Type serviceType)
    {
        var services = CreateServicesWithProjectRepository();
        services.AddApplicationServices();
        using var provider = services.BuildServiceProvider();

        var resolved = provider.GetRequiredService(serviceType);

        Assert.NotNull(resolved);
    }

    [Fact]
    public void AddApplicationServices_RegistersStateAndServices_AsSingletons()
    {
        var services = CreateServicesWithProjectRepository();
        services.AddApplicationServices();
        using var provider = services.BuildServiceProvider();

        var state1 = provider.GetRequiredService<IApplicationState>();
        var state2 = provider.GetRequiredService<IApplicationState>();
        var canvasState1 = provider.GetRequiredService<ICanvasState>();
        var canvasState2 = provider.GetRequiredService<ICanvasState>();
        var packetEngine1 = provider.GetRequiredService<NetSim.Core.Packets.IPacketEngine>();
        var packetEngine2 = provider.GetRequiredService<NetSim.Core.Packets.IPacketEngine>();
        var ethernet1 = provider.GetRequiredService<NetSim.Core.Ethernet.IEthernetTransmissionService>();
        var ethernet2 = provider.GetRequiredService<NetSim.Core.Ethernet.IEthernetTransmissionService>();
        var switching1 = provider.GetRequiredService<NetSim.Core.Switching.ISwitchingEngine>();
        var switching2 = provider.GetRequiredService<NetSim.Core.Switching.ISwitchingEngine>();
        var segment1 = provider.GetRequiredService<NetSim.Core.Switching.ISwitchedSegmentService>();
        var segment2 = provider.GetRequiredService<NetSim.Core.Switching.ISwitchedSegmentService>();
        var ipv41 = provider.GetRequiredService<NetSim.Core.IP.IIPv4Layer>();
        var ipv42 = provider.GetRequiredService<NetSim.Core.IP.IIPv4Layer>();
        var ipv61 = provider.GetRequiredService<NetSim.Core.IP.IIPv6Layer>();
        var ipv62 = provider.GetRequiredService<NetSim.Core.IP.IIPv6Layer>();
        var arp1 = provider.GetRequiredService<NetSim.Core.Arp.IArpLayer>();
        var arp2 = provider.GetRequiredService<NetSim.Core.Arp.IArpLayer>();
        var icmp1 = provider.GetRequiredService<NetSim.Core.Icmp.IIcmpLayer>();
        var icmp2 = provider.GetRequiredService<NetSim.Core.Icmp.IIcmpLayer>();
        var udpDelivery1 = provider.GetRequiredService<NetSim.Core.Udp.IUdpDeliveryManager>();
        var udpDelivery2 = provider.GetRequiredService<NetSim.Core.Udp.IUdpDeliveryManager>();
        var udp1 = provider.GetRequiredService<NetSim.Core.Udp.IUdpLayer>();
        var udp2 = provider.GetRequiredService<NetSim.Core.Udp.IUdpLayer>();
        var tcpConnections1 = provider.GetRequiredService<NetSim.Core.Tcp.ITcpConnectionManager>();
        var tcpConnections2 = provider.GetRequiredService<NetSim.Core.Tcp.ITcpConnectionManager>();
        var tcp1 = provider.GetRequiredService<NetSim.Core.Tcp.ITcpLayer>();
        var tcp2 = provider.GetRequiredService<NetSim.Core.Tcp.ITcpLayer>();
        var networkService1 = provider.GetRequiredService<INetworkService>();
        var networkService2 = provider.GetRequiredService<INetworkService>();
        var projectService1 = provider.GetRequiredService<IProjectService>();
        var projectService2 = provider.GetRequiredService<IProjectService>();
        var pingService1 = provider.GetRequiredService<NetSim.Application.Diagnostics.IPingService>();
        var pingService2 = provider.GetRequiredService<NetSim.Application.Diagnostics.IPingService>();
        var dnsCache1 = provider.GetRequiredService<NetSim.Core.Dns.IDnsCache>();
        var dnsCache2 = provider.GetRequiredService<NetSim.Core.Dns.IDnsCache>();
        var dnsServerService1 = provider.GetRequiredService<NetSim.Application.Dns.IDnsServerService>();
        var dnsServerService2 = provider.GetRequiredService<NetSim.Application.Dns.IDnsServerService>();
        var dnsResolver1 = provider.GetRequiredService<NetSim.Application.Dns.IDnsResolver>();
        var dnsResolver2 = provider.GetRequiredService<NetSim.Application.Dns.IDnsResolver>();
        var dhcpClientStore1 = provider.GetRequiredService<NetSim.Core.Dhcp.IDhcpClientStateStore>();
        var dhcpClientStore2 = provider.GetRequiredService<NetSim.Core.Dhcp.IDhcpClientStateStore>();
        var dhcpServerService1 = provider.GetRequiredService<NetSim.Application.Dhcp.IDhcpServerService>();
        var dhcpServerService2 = provider.GetRequiredService<NetSim.Application.Dhcp.IDhcpServerService>();
        var dhcpClient1 = provider.GetRequiredService<NetSim.Application.Dhcp.IDhcpClient>();
        var dhcpClient2 = provider.GetRequiredService<NetSim.Application.Dhcp.IDhcpClient>();

        Assert.Same(state1, state2);
        Assert.Same(canvasState1, canvasState2);
        Assert.Same(packetEngine1, packetEngine2);
        Assert.Same(ethernet1, ethernet2);
        Assert.Same(switching1, switching2);
        Assert.Same(segment1, segment2);
        Assert.Same(ipv41, ipv42);
        Assert.Same(ipv61, ipv62);
        Assert.Same(arp1, arp2);
        Assert.Same(icmp1, icmp2);
        Assert.Same(udpDelivery1, udpDelivery2);
        Assert.Same(udp1, udp2);
        Assert.Same(tcpConnections1, tcpConnections2);
        Assert.Same(tcp1, tcp2);
        Assert.Same(networkService1, networkService2);
        Assert.Same(projectService1, projectService2);
        Assert.Same(pingService1, pingService2);
        Assert.Same(dnsCache1, dnsCache2);
        Assert.Same(dnsServerService1, dnsServerService2);
        Assert.Same(dnsResolver1, dnsResolver2);
        Assert.Same(dhcpClientStore1, dhcpClientStore2);
        Assert.Same(dhcpServerService1, dhcpServerService2);
        Assert.Same(dhcpClient1, dhcpClient2);
    }
}
