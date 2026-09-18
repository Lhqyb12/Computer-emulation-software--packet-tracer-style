using NetSim.Core.Networking;
using NetSim.Core.Routing;

namespace NetSim.Core.Tests.Routing;

public class RouteTests
{
    [Fact]
    public void Connected_HasNoNextHop_IsDirectlyConnected_AndTakesTheInterfacesNetwork()
    {
        var lab = RoutingTestLab.WithOneInterface("192.168.1.1/24");

        var route = Route.Connected(lab.Gi0, IPv4Network.Parse("192.168.1.0/24"));

        Assert.Equal(RouteType.Connected, route.Type);
        Assert.True(route.IsDirectlyConnected);
        Assert.Null(route.NextHop);
        Assert.Same(lab.Gi0, route.OutgoingInterface);
        Assert.Equal(24, route.PrefixLength);
        Assert.Equal(0, route.AdministrativeDistance);
        Assert.False(route.IsDefault);
    }

    [Fact]
    public void IsActive_FollowsTheOutgoingInterfacesOperationalState()
    {
        var lab = RoutingTestLab.WithOneInterface("192.168.1.1/24");
        var route = Route.Connected(lab.Gi0, IPv4Network.Parse("192.168.1.0/24"));

        Assert.True(route.IsActive);

        lab.Gi0.BringDown();
        Assert.False(route.IsActive);

        lab.Gi0.BringUp();
        Assert.True(route.IsActive);
    }

    [Fact]
    public void Create_DefaultRoute_IsRecognisedAsDefault()
    {
        var route = Route.Create(IPv4Network.Parse("0.0.0.0/0"), RouteType.Default, nextHop: IPv4Address.Parse("192.0.2.1"));

        Assert.True(route.IsDefault);
        Assert.Equal(0, route.PrefixLength);
    }

    [Fact]
    public void Matches_UsesNetworkContainment()
    {
        var route = Route.Create(IPv4Network.Parse("10.10.0.0/16"), RouteType.Static, nextHop: IPv4Address.Parse("192.0.2.1"));

        Assert.True(route.Matches(IPv4Address.Parse("10.10.20.5")));
        Assert.False(route.Matches(IPv4Address.Parse("10.20.20.5")));
    }

    [Fact]
    public void ConnectedAdministrativeDistance_IsZero_StaticIsOne()
    {
        Assert.Equal(0, RouteType.Connected.DefaultAdministrativeDistance());
        Assert.Equal(1, RouteType.Static.DefaultAdministrativeDistance());
        Assert.Equal(120, RouteType.Rip.DefaultAdministrativeDistance());
        Assert.Equal(110, RouteType.Ospf.DefaultAdministrativeDistance());
    }
}
