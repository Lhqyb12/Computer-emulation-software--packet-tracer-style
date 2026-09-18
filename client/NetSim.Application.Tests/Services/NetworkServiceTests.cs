using NetSim.Application.Services;
using NetSim.Application.State;
using NetSim.Core.Devices;
using NetSim.Core.Networking;

namespace NetSim.Application.Tests.Services;

public class NetworkServiceTests
{
    [Fact]
    public void TopologyQuery_IsAvailable_AndOperatesOnTheCurrentNetwork()
    {
        var service = new NetworkService(new ApplicationState());
        var network = service.CreateNetwork("Lab");

        var a = new Router("A");
        var b = new Router("B");
        var ia = a.AddInterface("G0/0", InterfaceType.GigabitEthernet);
        var ib = b.AddInterface("G0/0", InterfaceType.GigabitEthernet);
        network.AddDevice(a);
        network.AddDevice(b);
        network.Connect(ia, ib);

        Assert.NotNull(service.TopologyQuery);
        Assert.True(service.TopologyQuery.HasPath(network, a, b));
    }

    [Fact]
    public void CurrentNetwork_IsNull_BeforeAnyNetworkCreated()
    {
        var service = new NetworkService(new ApplicationState());

        Assert.Null(service.CurrentNetwork);
    }

    [Fact]
    public void CreateNetwork_ReturnsNetwork_AndMakesItCurrent()
    {
        var service = new NetworkService(new ApplicationState());

        var network = service.CreateNetwork("Office Network");

        Assert.Equal("Office Network", network.Name);
        Assert.Same(network, service.CurrentNetwork);
    }

    [Fact]
    public void CreateNetwork_Twice_ReplacesCurrentNetwork()
    {
        var service = new NetworkService(new ApplicationState());
        service.CreateNetwork("First");

        var second = service.CreateNetwork("Second");

        Assert.Same(second, service.CurrentNetwork);
    }

    [Fact]
    public void ClearNetwork_ResetsCurrentNetworkToNull()
    {
        var service = new NetworkService(new ApplicationState());
        service.CreateNetwork("Office Network");

        service.ClearNetwork();

        Assert.Null(service.CurrentNetwork);
    }
}
