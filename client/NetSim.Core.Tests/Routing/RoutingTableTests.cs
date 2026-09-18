using System.Linq;
using NetSim.Core.Common.Exceptions;
using NetSim.Core.Networking;
using NetSim.Core.Routing;

namespace NetSim.Core.Tests.Routing;

public class RoutingTableTests
{
    private static Route Static(string cidr, string nextHop) =>
        Route.Create(IPv4Network.Parse(cidr), RouteType.Static, nextHop: IPv4Address.Parse(nextHop));

    [Fact]
    public void FindBestRoute_ExactNetwork_And_HostWithinNetwork()
    {
        var table = new RoutingTable();
        table.AddRoute(Static("192.168.2.0/24", "10.0.0.1"));

        Assert.True(table.FindBestRoute(IPv4Address.Parse("192.168.2.0")).HasRoute);
        Assert.True(table.FindBestRoute(IPv4Address.Parse("192.168.2.50")).HasRoute);
        Assert.Equal(IPv4Network.Parse("192.168.2.0/24"),
            table.FindBestRoute(IPv4Address.Parse("192.168.2.50")).Route!.Destination);
    }

    [Fact]
    public void FindBestRoute_NoMatchAndNoDefault_ReturnsNoRoute()
    {
        var table = new RoutingTable();
        table.AddRoute(Static("192.168.1.0/24", "10.0.0.1"));
        table.AddRoute(Static("192.168.2.0/24", "10.0.0.1"));

        var result = table.FindBestRoute(IPv4Address.Parse("10.10.10.10"));

        Assert.False(result.HasRoute);
        Assert.Null(result.Route);
        Assert.Null(result.OutgoingInterface);
        Assert.Equal(-1, result.PrefixLength);
    }

    [Fact]
    public void FindBestRoute_DefaultRoute_SelectedWhenNoMoreSpecificMatch_ButLosesToLongerPrefix()
    {
        var table = new RoutingTable();
        table.AddRoute(Static("192.168.1.0/24", "10.0.0.1"));
        table.AddRoute(Static("0.0.0.0/0", "10.0.0.254"));

        Assert.Equal(IPv4Network.Parse("0.0.0.0/0"),
            table.FindBestRoute(IPv4Address.Parse("8.8.8.8")).Route!.Destination);

        table.AddRoute(Static("8.8.8.0/24", "10.0.0.2"));
        Assert.Equal(IPv4Network.Parse("8.8.8.0/24"),
            table.FindBestRoute(IPv4Address.Parse("8.8.8.8")).Route!.Destination);
    }

    [Fact]
    public void NextHopAddress_IsTheDestinationItself_ForADirectlyConnectedRoute()
    {
        var lab = RoutingTestLab.WithOneInterface("192.168.2.1/24");

        var result = lab.Router.RoutingTable.FindBestRoute(IPv4Address.Parse("192.168.2.50"));

        Assert.True(result.IsDirectlyConnected);
        Assert.Equal(IPv4Address.Parse("192.168.2.50"), result.NextHopAddress);
        Assert.Same(lab.Gi0, result.OutgoingInterface);
    }

    [Fact]
    public void AddRoute_RejectsAConnectedRouteWithoutAnInterface()
    {
        var table = new RoutingTable();
        Assert.Throws<DomainException>(() =>
            table.AddRoute(Route.Create(IPv4Network.Parse("10.0.0.0/24"), RouteType.Connected)));
    }

    [Fact]
    public void AddRoute_RejectsANonConnectedRouteWithNeitherInterfaceNorNextHop()
    {
        var table = new RoutingTable();
        Assert.Throws<DomainException>(() =>
            table.AddRoute(Route.Create(IPv4Network.Parse("10.0.0.0/24"), RouteType.Static)));
    }

    [Fact]
    public void RemoveRoute_RemovesExactlyOnePrefix()
    {
        var table = new RoutingTable();
        table.AddRoute(Static("192.168.1.0/24", "10.0.0.1"));
        table.AddRoute(Static("192.168.2.0/24", "10.0.0.1"));

        Assert.True(table.RemoveRoute(IPv4Network.Parse("192.168.1.0/24")));
        Assert.False(table.RemoveRoute(IPv4Network.Parse("192.168.1.0/24")));
        Assert.Single(table.GetRoutes());
    }

    [Fact]
    public void GetRoutesForInterface_ReturnsOnlyThatInterfacesRoutes()
    {
        var lab = RoutingTestLab.WithTwoInterfaces("192.168.1.1/24", "192.168.2.1/24");

        var gi0Routes = lab.Router.RoutingTable.GetRoutesForInterface(lab.Gi0);

        Assert.Single(gi0Routes);
        Assert.Equal(IPv4Network.Parse("192.168.1.0/24"), gi0Routes[0].Destination);
    }

    [Fact]
    public void ReplaceConnectedRoutes_LeavesNonConnectedRoutesUntouched()
    {
        var lab = RoutingTestLab.WithOneInterface("192.168.1.1/24");
        lab.Router.RoutingTable.AddRoute(Static("0.0.0.0/0", "192.168.1.254"));

        // Re-derive connected routes (as an address change would): the static default route survives.
        lab.Router.SyncConnectedRoutes();

        var routes = lab.Router.RoutingTable.GetRoutes();
        Assert.Contains(routes, r => r.Type == RouteType.Connected && r.Destination == IPv4Network.Parse("192.168.1.0/24"));
        Assert.Contains(routes, r => r.Type == RouteType.Static && r.IsDefault);
    }

    [Fact]
    public void RoutesChanged_RaisedOnMutation()
    {
        var table = new RoutingTable();
        var count = 0;
        table.RoutesChanged += (_, _) => count++;

        table.AddRoute(Static("10.0.0.0/8", "192.0.2.1"));
        table.RemoveRoute(IPv4Network.Parse("10.0.0.0/8"));

        Assert.Equal(2, count);
    }
}
