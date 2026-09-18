using NetSim.Core.Devices;
using NetSim.Core.Dns;
using NetSim.Core.Networking;

namespace NetSim.Core.Tests.Dns;

public class DnsClientConfigurationStoreTests
{
    private static NetworkDevice CreateDevice() => NetworkDeviceFactory.Create(DeviceType.Pc, "PC0");

    [Fact]
    public void GetServers_BeforeConfiguration_IsEmpty()
    {
        var store = new DnsClientConfigurationStore();

        Assert.Empty(store.GetServers(CreateDevice()));
    }

    [Fact]
    public void SetServers_ThenGetServers_ReturnsThem()
    {
        var store = new DnsClientConfigurationStore();
        var device = CreateDevice();
        var server = IPv4Address.Parse("192.168.1.53");

        store.SetServers(device, [server]);

        Assert.Equal([server], store.GetServers(device));
    }

    [Fact]
    public void AddServer_Twice_DoesNotDuplicate()
    {
        var store = new DnsClientConfigurationStore();
        var device = CreateDevice();
        var server = IPv4Address.Parse("192.168.1.53");

        store.AddServer(device, server);
        store.AddServer(device, server);

        Assert.Single(store.GetServers(device));
    }

    [Fact]
    public void RemoveServer_DropsIt()
    {
        var store = new DnsClientConfigurationStore();
        var device = CreateDevice();
        var server = IPv4Address.Parse("192.168.1.53");
        store.AddServer(device, server);

        Assert.True(store.RemoveServer(device, server));
        Assert.Empty(store.GetServers(device));
    }

    [Fact]
    public void DifferentDevices_HaveIndependentConfiguration()
    {
        var store = new DnsClientConfigurationStore();
        var device1 = CreateDevice();
        var device2 = CreateDevice();

        store.AddServer(device1, IPv4Address.Parse("192.168.1.53"));

        Assert.Single(store.GetServers(device1));
        Assert.Empty(store.GetServers(device2));
    }
}
