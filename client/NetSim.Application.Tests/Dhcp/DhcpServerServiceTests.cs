using NetSim.Application.Dhcp;
using NetSim.Core.Common.Exceptions;
using NetSim.Core.Devices;
using NetSim.Core.Dhcp;
using NetSim.Core.Networking;
using NetSim.Core.Udp;

namespace NetSim.Application.Tests.Dhcp;

public class DhcpServerServiceTests
{
    private static NetworkDevice CreateDevice() => NetworkDeviceFactory.Create(DeviceType.Server, "Server0");

    private static IDhcpServer CreateServer() => new DhcpServer(new DhcpServerConfiguration(
        IPv4Address.Parse("192.168.1.1"), 24, IPv4Address.Parse("192.168.1.100"), IPv4Address.Parse("192.168.1.200")));

    private static (DhcpServerService Service, UdpDeliveryManager Udp) Build()
    {
        var udp = new UdpDeliveryManager();
        return (new DhcpServerService(udp), udp);
    }

    [Fact]
    public void Start_BindsUdpPort67()
    {
        var (service, udp) = Build();
        var device = CreateDevice();

        service.Start(device, CreateServer());

        Assert.True(udp.IsBound(device, DhcpProtocol.ServerPort));
    }

    [Fact]
    public void Start_Twice_OnTheSameDevice_Throws()
    {
        var (service, _) = Build();
        var device = CreateDevice();
        service.Start(device, CreateServer());

        Assert.Throws<DomainException>(() => service.Start(device, CreateServer()));
    }

    [Fact]
    public void IsRunning_ReflectsStartAndStop()
    {
        var (service, _) = Build();
        var device = CreateDevice();

        Assert.False(service.IsRunning(device));
        service.Start(device, CreateServer());
        Assert.True(service.IsRunning(device));
        service.Stop(device);
        Assert.False(service.IsRunning(device));
    }

    [Fact]
    public void Stop_ReleasesTheUdpBinding()
    {
        var (service, udp) = Build();
        var device = CreateDevice();
        service.Start(device, CreateServer());

        Assert.True(service.Stop(device));
        Assert.False(udp.IsBound(device, DhcpProtocol.ServerPort));
    }

    [Fact]
    public void Stop_WhenNotRunning_ReturnsFalse()
    {
        var (service, _) = Build();

        Assert.False(service.Stop(CreateDevice()));
    }

    [Fact]
    public void TryGetServer_ReturnsTheHostedInstance()
    {
        var (service, _) = Build();
        var device = CreateDevice();
        var server = CreateServer();

        service.Start(device, server);

        Assert.True(service.TryGetServer(device, out var found));
        Assert.Same(server, found);
    }
}
