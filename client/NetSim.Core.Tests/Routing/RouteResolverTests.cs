using System.Collections.Generic;
using System.Linq;
using NetSim.Core.Networking;
using NetSim.Core.Routing;

namespace NetSim.Core.Tests.Routing;

/// <summary>
/// Phase 28 sections 10-11, 27: recursive next-hop resolution. A static route with a next hop but
/// no exit interface is resolved by walking the table down to a directly connected route; the
/// resolved interface is that connected route's interface, and the address ARP resolves is the
/// original next hop - never the final destination. Loops and runaway recursion are contained.
/// </summary>
public class RouteResolverTests
{
    private static Route Connected(NetworkInterface nic, string cidr) =>
        Route.Connected(nic, IPv4Network.Parse(cidr));

    private static Route Static(string cidr, string nextHop, NetworkInterface? nic = null) =>
        Route.Create(IPv4Network.Parse(cidr), RouteType.Static, nic, IPv4Address.Parse(nextHop));

    [Fact]
    public void Resolve_NextHopRoute_UsesTheConnectedRoutesInterface_AndArpsForTheNextHop()
    {
        var lab = RoutingTestLab.WithTwoInterfaces("192.168.1.1/24", "10.0.0.1/30");
        var connected = lab.Router.RoutingTable.GetRoutes().ToList();
        var staticRoute = Static("192.168.2.0/24", "10.0.0.2");
        var all = connected.Append(staticRoute).ToList();

        var resolution = RouteResolver.Resolve(all, staticRoute, IPv4Address.Parse("192.168.2.10"));

        Assert.NotNull(resolution);
        Assert.Same(lab.Gi1, resolution!.OutgoingInterface);
        Assert.Equal(IPv4Address.Parse("10.0.0.2"), resolution.L2NextHopAddress);   // NOT 192.168.2.10
        Assert.Same(staticRoute, resolution.MatchedRoute);
    }

    [Fact]
    public void Resolve_ExitInterfaceRoute_ArpsForTheFinalDestinationOnThatInterface()
    {
        var lab = RoutingTestLab.WithTwoInterfaces("192.168.1.1/24", "10.0.0.1/30");
        var exitOnly = Route.Create(IPv4Network.Parse("192.168.9.0/24"), RouteType.Static, outgoingInterface: lab.Gi1);
        var all = lab.Router.RoutingTable.GetRoutes().Append(exitOnly).ToList();

        var resolution = RouteResolver.Resolve(all, exitOnly, IPv4Address.Parse("192.168.9.7"));

        Assert.NotNull(resolution);
        Assert.Same(lab.Gi1, resolution!.OutgoingInterface);
        Assert.Equal(IPv4Address.Parse("192.168.9.7"), resolution.L2NextHopAddress);
    }

    [Fact]
    public void Resolve_FullySpecifiedRoute_TrustsTheInterface_AndArpsForTheStatedNextHop()
    {
        var lab = RoutingTestLab.WithTwoInterfaces("192.168.1.1/24", "10.0.0.1/30");
        var fully = Route.Create(
            IPv4Network.Parse("172.16.0.0/16"), RouteType.Static, lab.Gi1, IPv4Address.Parse("10.0.0.2"));
        var all = lab.Router.RoutingTable.GetRoutes().Append(fully).ToList();

        var resolution = RouteResolver.Resolve(all, fully, IPv4Address.Parse("172.16.5.5"));

        Assert.Same(lab.Gi1, resolution!.OutgoingInterface);
        Assert.Equal(IPv4Address.Parse("10.0.0.2"), resolution.L2NextHopAddress);
    }

    [Fact]
    public void Resolve_ReturnsNull_WhenTheNextHopIsNotReachable()
    {
        var lab = RoutingTestLab.WithTwoInterfaces("192.168.1.1/24", "10.0.0.1/30");
        var unreachable = Static("192.168.2.0/24", "203.0.113.9");
        var all = lab.Router.RoutingTable.GetRoutes().Append(unreachable).ToList();

        Assert.Null(RouteResolver.Resolve(all, unreachable, IPv4Address.Parse("192.168.2.10")));
    }

    [Fact]
    public void Resolve_ReturnsNull_WhenTheTransitInterfaceIsDown()
    {
        var lab = RoutingTestLab.WithTwoInterfaces("192.168.1.1/24", "10.0.0.1/30");
        var staticRoute = Static("192.168.2.0/24", "10.0.0.2");
        var all = lab.Router.RoutingTable.GetRoutes().Append(staticRoute).ToList();
        lab.Gi1.BringDown();

        Assert.Null(RouteResolver.Resolve(all, staticRoute, IPv4Address.Parse("192.168.2.10")));
    }

    [Fact]
    public void Resolve_DetectsANextHopCycle_WithoutSpinning()
    {
        // 192.168.2.0/24 -> via 10.0.0.2, and the only route to 10.0.0.2 is 10.0.0.0/24 -> via
        // 192.168.2.1: a two-route loop with no connected anchor.
        var a = Static("192.168.2.0/24", "10.0.0.2");
        var b = Static("10.0.0.0/24", "192.168.2.1");

        Assert.Null(RouteResolver.Resolve(new List<Route> { a, b }, a, IPv4Address.Parse("192.168.2.10")));
    }

    [Fact]
    public void Resolve_DirectlyConnectedRoute_ArpsForTheDestinationItself()
    {
        var lab = RoutingTestLab.WithOneInterface("192.168.1.1/24");
        var connected = lab.Router.RoutingTable.GetRoutes().Single();

        var resolution = RouteResolver.Resolve(new[] { connected }, connected, IPv4Address.Parse("192.168.1.50"));

        Assert.Same(lab.Gi0, resolution!.OutgoingInterface);
        Assert.Equal(IPv4Address.Parse("192.168.1.50"), resolution.L2NextHopAddress);
    }
}
