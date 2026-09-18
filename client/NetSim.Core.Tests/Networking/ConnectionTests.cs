using NetSim.Core.Common.Exceptions;
using NetSim.Core.Devices;
using NetSim.Core.Networking;

namespace NetSim.Core.Tests.Networking;

public class ConnectionTests
{
    private static (NetworkInterface, NetworkInterface) CreateTwoInterfaces()
    {
        var router1 = new Router("Router1");
        var router2 = new Router("Router2");
        var ifaceA = router1.AddInterface("G0/0", InterfaceType.GigabitEthernet);
        var ifaceB = router2.AddInterface("G0/0", InterfaceType.GigabitEthernet);
        return (ifaceA, ifaceB);
    }

    [Fact]
    public void Create_ConnectsBothEndpoints()
    {
        var (ifaceA, ifaceB) = CreateTwoInterfaces();

        var connection = Connection.Create(ifaceA, ifaceB);

        Assert.Same(ifaceA, connection.EndpointA);
        Assert.Same(ifaceB, connection.EndpointB);
        Assert.Same(connection, ifaceA.Connection);
        Assert.Same(connection, ifaceB.Connection);
        Assert.True(ifaceA.IsConnected);
        Assert.True(ifaceB.IsConnected);
    }

    [Fact]
    public void Create_RejectsConnectingInterfaceToItself()
    {
        var router = new Router("Router1");
        var iface = router.AddInterface("G0/0", InterfaceType.GigabitEthernet);

        Assert.Throws<DomainException>(() => Connection.Create(iface, iface));
    }

    [Fact]
    public void Create_RejectsEndpointThatIsAlreadyConnected()
    {
        var (ifaceA, ifaceB) = CreateTwoInterfaces();
        Connection.Create(ifaceA, ifaceB);

        var router3 = new Router("Router3");
        var ifaceC = router3.AddInterface("G0/0", InterfaceType.GigabitEthernet);

        Assert.Throws<DomainException>(() => Connection.Create(ifaceA, ifaceC));
    }

    [Fact]
    public void GetOtherEndpoint_ReturnsTheOppositeSide()
    {
        var (ifaceA, ifaceB) = CreateTwoInterfaces();
        var connection = Connection.Create(ifaceA, ifaceB);

        Assert.Same(ifaceB, connection.GetOtherEndpoint(ifaceA));
        Assert.Same(ifaceA, connection.GetOtherEndpoint(ifaceB));
    }

    [Fact]
    public void Disconnect_DetachesBothEndpoints()
    {
        var (ifaceA, ifaceB) = CreateTwoInterfaces();
        var connection = Connection.Create(ifaceA, ifaceB);

        connection.Disconnect();

        Assert.False(ifaceA.IsConnected);
        Assert.False(ifaceB.IsConnected);
        Assert.Null(ifaceA.Connection);
        Assert.Null(ifaceB.Connection);
    }

    [Fact]
    public void Create_AssignsAUniqueIdentity()
    {
        var (a1, b1) = CreateTwoInterfaces();
        var (a2, b2) = CreateTwoInterfaces();

        var first = Connection.Create(a1, b1);
        var second = Connection.Create(a2, b2);

        Assert.NotEqual(default, first.Id);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void Create_DefaultsToCopperConnectionType()
    {
        var (ifaceA, ifaceB) = CreateTwoInterfaces();

        var connection = Connection.Create(ifaceA, ifaceB);

        Assert.Equal(ConnectionType.Copper, connection.ConnectionType);
    }

    [Fact]
    public void Create_AllowsACableToAnAdministrativelyDisabledPort_ItIsJustNotOperational()
    {
        // A disabled port is an L2/L3 config state; a cable (L1) can still be run to it. Refusing
        // that is a workflow rule (Connect tool / IConnectionService), not a domain invariant -
        // and keeping the domain permissive is what lets a saved topology with a shut, cabled
        // interface rehydrate. See Connection.Create.
        var (ifaceA, ifaceB) = CreateTwoInterfaces();
        ifaceB.Disable();

        var connection = Connection.Create(ifaceA, ifaceB);

        Assert.True(ifaceA.IsConnected);
        Assert.True(ifaceB.IsConnected);
        Assert.False(ifaceB.IsOperational);
    }

    [Fact]
    public void Create_RejectsIncompatibleInterfaceTypes()
    {
        var router = new Router("R1");
        var pc = new Pc("PC1");
        var serial = router.AddInterface("S0/0", InterfaceType.Serial);
        var ethernet = pc.AddInterface("Eth0", InterfaceType.Ethernet);

        var ex = Assert.Throws<DomainException>(() => Connection.Create(serial, ethernet));
        Assert.Contains("cannot be connected", ex.Message);
        Assert.False(serial.IsConnected);
        Assert.False(ethernet.IsConnected);
    }
}
