using System.Linq;
using NetSim.Core.Devices;
using NetSim.Core.Networking;
using NetSim.Core.Routing;

namespace NetSim.Core.Tests.Devices;

/// <summary>
/// Phase 27 sections 10-11, 27-28, 46: a router's connected routes are derived from its interface
/// IPv4 configuration, react to address / interface-state changes, and never leave a stale entry.
/// </summary>
public class RouterRoutingTableTests
{
    private static NetworkInterface AddUp(Router router, string name)
    {
        var networkInterface = router.AddInterface(name, InterfaceType.GigabitEthernet);
        networkInterface.BringUp();
        return networkInterface;
    }

    private static void SetIp(NetworkInterface networkInterface, string cidr)
    {
        var slash = cidr.IndexOf('/');
        networkInterface.SetPrimaryIPv4Configuration(
            Ipv4InterfaceConfiguration.Create(IPv4Address.Parse(cidr[..slash]), int.Parse(cidr[(slash + 1)..])));
        ((Router)networkInterface.Device).SyncConnectedRoutes();
    }

    [Fact]
    public void ConfiguringAnInterfaceAddress_CreatesTheConnectedRoute()
    {
        var router = new Router("R1");
        var gi0 = AddUp(router, "GigabitEthernet0/0");
        var gi1 = AddUp(router, "GigabitEthernet0/1");

        SetIp(gi0, "192.168.1.1/24");
        SetIp(gi1, "192.168.2.1/24");

        var routes = router.RoutingTable.GetRoutes();
        Assert.Equal(2, routes.Count);
        Assert.Contains(routes, r => r.Destination == IPv4Network.Parse("192.168.1.0/24") && r.OutgoingInterface == gi0 && r.Type == RouteType.Connected);
        Assert.Contains(routes, r => r.Destination == IPv4Network.Parse("192.168.2.0/24") && r.OutgoingInterface == gi1);
    }

    [Fact]
    public void ChangingAnInterfaceAddress_RemovesTheStaleConnectedRoute_AndAddsTheNewOne()
    {
        var router = new Router("R1");
        var gi0 = AddUp(router, "GigabitEthernet0/0");
        SetIp(gi0, "192.168.1.1/24");

        SetIp(gi0, "10.0.0.1/24");

        var routes = router.RoutingTable.GetRoutes();
        Assert.Single(routes);
        Assert.Equal(IPv4Network.Parse("10.0.0.0/24"), routes[0].Destination);
        Assert.DoesNotContain(routes, r => r.Destination == IPv4Network.Parse("192.168.1.0/24"));
    }

    [Fact]
    public void ClearingAnInterfaceAddress_RemovesItsConnectedRoute()
    {
        var router = new Router("R1");
        var gi0 = AddUp(router, "GigabitEthernet0/0");
        SetIp(gi0, "192.168.1.1/24");

        gi0.ClearIPv4Configuration();
        router.SyncConnectedRoutes();

        Assert.Empty(router.RoutingTable.GetRoutes());
    }

    [Fact]
    public void AnInterfaceGoingDown_LeavesTheRouteConfigured_ButInactive()
    {
        var router = new Router("R1");
        var gi0 = AddUp(router, "GigabitEthernet0/0");
        SetIp(gi0, "192.168.1.1/24");

        gi0.BringDown();

        var route = Assert.Single(router.RoutingTable.GetRoutes());
        Assert.False(route.IsActive);
        Assert.False(router.RoutingTable.FindBestRoute(IPv4Address.Parse("192.168.1.5")).HasRoute);

        gi0.BringUp();
        Assert.True(router.RoutingTable.GetRoutes()[0].IsActive);
        Assert.True(router.RoutingTable.FindBestRoute(IPv4Address.Parse("192.168.1.5")).HasRoute);
    }

    [Fact]
    public void ValidateAddressing_FlagsTwoInterfacesInTheSameNetwork()
    {
        var router = new Router("R1");
        var gi0 = AddUp(router, "GigabitEthernet0/0");
        var gi1 = AddUp(router, "GigabitEthernet0/1");
        SetIp(gi0, "192.168.1.1/24");
        SetIp(gi1, "192.168.1.2/24");

        var issues = router.ValidateAddressing();

        Assert.Single(issues);
        Assert.Contains("192.168.1.0/24", issues[0]);

        // Only the first interface's connected route is kept - routing stays unambiguous.
        Assert.Single(router.RoutingTable.GetRoutes());
    }

    [Fact]
    public void ValidateAddressing_IsCleanForSeparateNetworks()
    {
        var router = new Router("R1");
        var gi0 = AddUp(router, "GigabitEthernet0/0");
        var gi1 = AddUp(router, "GigabitEthernet0/1");
        SetIp(gi0, "192.168.1.1/24");
        SetIp(gi1, "192.168.2.1/24");

        Assert.Empty(router.ValidateAddressing());
    }

    [Fact]
    public void ARouterHasNoSwitchStyleMacAddressTable()
    {
        // Guards the section 56 architectural boundary: Router must not grow a MAC learning table.
        Assert.DoesNotContain(
            typeof(Router).GetProperties(),
            p => p.Name.Contains("MacAddressTable"));
    }
}
