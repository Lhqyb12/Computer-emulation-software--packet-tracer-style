using NetSim.Application.Dns;
using NetSim.Core.Common.Exceptions;
using NetSim.Core.Devices;
using NetSim.Core.Dns;
using NetSim.Core.Tcp;
using NetSim.Core.Transport;
using NetSim.Core.Udp;

namespace NetSim.Application.Tests.Dns;

public class DnsServerServiceTests
{
    private static NetworkDevice CreateDevice() => NetworkDeviceFactory.Create(DeviceType.Server, "Server0");

    private static (DnsServerService Service, IUdpDeliveryManager Udp, ITcpConnectionManager Tcp) BuildService()
    {
        var udp = new UdpDeliveryManager();
        var tcp = new TcpConnectionManager(new RandomInitialSequenceNumberGenerator());
        return (new DnsServerService(udp, tcp), udp, tcp);
    }

    [Fact]
    public void Start_BindsUdpPort53()
    {
        var (service, udp, _) = BuildService();
        var device = CreateDevice();

        service.Start(device, new DnsServer(DomainName.Parse("example.com")));

        Assert.True(udp.IsBound(device, DnsProtocol.Port));
    }

    [Fact]
    public void Start_WithTcpEnabled_AlsoListensOnTcpPort53()
    {
        var (service, _, tcp) = BuildService();
        var device = CreateDevice();

        service.Start(device, new DnsServer(DomainName.Parse("example.com")), enableTcp: true);

        Assert.True(tcp.IsListening(device, DnsProtocol.Port));
    }

    [Fact]
    public void Start_WithTcpDisabled_DoesNotListenOnTcp()
    {
        var (service, _, tcp) = BuildService();
        var device = CreateDevice();

        service.Start(device, new DnsServer(DomainName.Parse("example.com")), enableTcp: false);

        Assert.False(tcp.IsListening(device, DnsProtocol.Port));
    }

    [Fact]
    public void Start_Twice_OnTheSameDevice_Throws()
    {
        var (service, _, _) = BuildService();
        var device = CreateDevice();
        service.Start(device, new DnsServer(DomainName.Parse("example.com")));

        Assert.Throws<DomainException>(() => service.Start(device, new DnsServer(DomainName.Parse("example.com"))));
    }

    [Fact]
    public void IsRunning_ReflectsStartAndStop()
    {
        var (service, _, _) = BuildService();
        var device = CreateDevice();

        Assert.False(service.IsRunning(device));

        service.Start(device, new DnsServer(DomainName.Parse("example.com")));
        Assert.True(service.IsRunning(device));

        service.Stop(device);
        Assert.False(service.IsRunning(device));
    }

    [Fact]
    public void Stop_ReleasesTheUdpBinding()
    {
        var (service, udp, _) = BuildService();
        var device = CreateDevice();
        service.Start(device, new DnsServer(DomainName.Parse("example.com")));

        var stopped = service.Stop(device);

        Assert.True(stopped);
        Assert.False(udp.IsBound(device, DnsProtocol.Port));
    }

    [Fact]
    public void Stop_WhenNotRunning_ReturnsFalse()
    {
        var (service, _, _) = BuildService();

        Assert.False(service.Stop(CreateDevice()));
    }

    [Fact]
    public void TryGetServer_ReturnsTheHostedServerInstance()
    {
        var (service, _, _) = BuildService();
        var device = CreateDevice();
        var dnsServer = new DnsServer(DomainName.Parse("example.com"));

        service.Start(device, dnsServer);

        Assert.True(service.TryGetServer(device, out var found));
        Assert.Same(dnsServer, found);
    }

    [Fact]
    public void TryGetServer_WhenNotRunning_ReturnsFalse()
    {
        var (service, _, _) = BuildService();

        Assert.False(service.TryGetServer(CreateDevice(), out var found));
        Assert.Null(found);
    }
}
