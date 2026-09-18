using NetSim.Core.Common.Exceptions;
using NetSim.Core.Devices;
using NetSim.Core.Networking;
using NetSim.Core.Topology;

namespace NetSim.Core.Tests.Topology;

public class NetworkTests
{
    [Fact]
    public void AddDevice_AddsToNetworkDeviceCollection()
    {
        var network = new Network("Lab1");
        var router = new Router("Router1");

        network.AddDevice(router);

        Assert.Contains(router, network.Devices);
    }

    [Fact]
    public void AddDevice_RejectsDuplicateDeviceId()
    {
        var network = new Network("Lab1");
        var router = new Router("Router1");
        network.AddDevice(router);

        Assert.Throws<DomainException>(() => network.AddDevice(router));
    }

    [Fact]
    public void Connect_CreatesAndTracksConnection()
    {
        var network = new Network("Lab1");
        var router = new Router("Router1");
        var sw = new Switch("Switch1");
        network.AddDevice(router);
        network.AddDevice(sw);
        var routerIface = router.AddInterface("G0/0", InterfaceType.GigabitEthernet);
        var switchIface = sw.AddInterface("Port1", InterfaceType.FastEthernet);

        var connection = network.Connect(routerIface, switchIface);

        Assert.Contains(connection, network.Connections);
    }

    [Fact]
    public void Connect_BringsTheNewLinkUp()
    {
        var network = new Network("Lab1");
        var pc = new Pc("PC1");
        var sw = new Switch("Switch1");
        network.AddDevice(pc);
        network.AddDevice(sw);
        var pcIface = pc.AddInterface("Eth0", InterfaceType.Ethernet);
        var switchIface = sw.AddInterface("Fa0/1", InterfaceType.FastEthernet);

        var connection = network.Connect(pcIface, switchIface);

        Assert.Equal(ConnectionState.Up, connection.State);
    }

    [Fact]
    public void Connect_RejectsASecondConnectionBetweenTheSameTwoInterfaces()
    {
        var network = new Network("Lab1");
        var pc = new Pc("PC1");
        var sw = new Switch("Switch1");
        network.AddDevice(pc);
        network.AddDevice(sw);
        var pcIface = pc.AddInterface("Eth0", InterfaceType.Ethernet);
        var switchIface = sw.AddInterface("Fa0/1", InterfaceType.FastEthernet);
        network.Connect(pcIface, switchIface);

        Assert.Throws<DomainException>(() => network.Connect(pcIface, switchIface));
        Assert.Single(network.Connections);
    }

    [Fact]
    public void FindConnection_ReturnsTheLinkBetweenTwoInterfaces_OrderIndependently()
    {
        var network = new Network("Lab1");
        var pc = new Pc("PC1");
        var sw = new Switch("Switch1");
        network.AddDevice(pc);
        network.AddDevice(sw);
        var pcIface = pc.AddInterface("Eth0", InterfaceType.Ethernet);
        var switchIface = sw.AddInterface("Fa0/1", InterfaceType.FastEthernet);
        var connection = network.Connect(pcIface, switchIface);

        Assert.Same(connection, network.FindConnection(pcIface, switchIface));
        Assert.Same(connection, network.FindConnection(switchIface, pcIface));
    }

    [Fact]
    public void Connect_RejectsEndpointFromDeviceNotInNetwork()
    {
        var network = new Network("Lab1");
        var router = new Router("Router1");
        network.AddDevice(router);
        var routerIface = router.AddInterface("G0/0", InterfaceType.GigabitEthernet);

        var outsideDevice = new Switch("Switch1");
        var outsideIface = outsideDevice.AddInterface("Port1", InterfaceType.FastEthernet);

        Assert.Throws<DomainException>(() => network.Connect(routerIface, outsideIface));
    }

    [Fact]
    public void RemoveConnection_DetachesEndpointsAndForgetsConnection()
    {
        var network = new Network("Lab1");
        var router = new Router("Router1");
        var sw = new Switch("Switch1");
        network.AddDevice(router);
        network.AddDevice(sw);
        var routerIface = router.AddInterface("G0/0", InterfaceType.GigabitEthernet);
        var switchIface = sw.AddInterface("Port1", InterfaceType.FastEthernet);
        var connection = network.Connect(routerIface, switchIface);

        var removed = network.RemoveConnection(connection);

        Assert.True(removed);
        Assert.DoesNotContain(connection, network.Connections);
        Assert.False(routerIface.IsConnected);
        Assert.False(switchIface.IsConnected);
    }

    [Fact]
    public void RemoveDevice_AlsoRemovesItsConnections()
    {
        var network = new Network("Lab1");
        var router = new Router("Router1");
        var sw = new Switch("Switch1");
        network.AddDevice(router);
        network.AddDevice(sw);
        var routerIface = router.AddInterface("G0/0", InterfaceType.GigabitEthernet);
        var switchIface = sw.AddInterface("Port1", InterfaceType.FastEthernet);
        var connection = network.Connect(routerIface, switchIface);

        var removed = network.RemoveDevice(router);

        Assert.True(removed);
        Assert.DoesNotContain(router, network.Devices);
        Assert.DoesNotContain(connection, network.Connections);
        Assert.False(switchIface.IsConnected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_RejectsInvalidName(string? name)
    {
        Assert.Throws<DomainException>(() => new Network(name!));
    }
}
