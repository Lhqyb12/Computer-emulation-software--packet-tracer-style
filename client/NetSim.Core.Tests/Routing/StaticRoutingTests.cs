using System;
using System.Linq;
using NetSim.Core.Common.Exceptions;
using NetSim.Core.IP;
using NetSim.Core.Networking;
using NetSim.Core.Routing;

namespace NetSim.Core.Tests.Routing;

/// <summary>
/// Phase 28 sections 3, 5-8, 16, 30: static routes in the routing table - the model carries an
/// identity, they take part in the one longest-prefix lookup alongside connected routes, a default
/// static route is just <c>0.0.0.0/0</c>, a more specific route always wins, and a matched route
/// whose path is currently down yields "no usable route" without hiding that it exists.
/// </summary>
public class StaticRoutingTests
{
    private static IPv4Packet Packet(string source, string destination, byte ttl = 64) =>
        IPv4Packet.Create(IPv4Address.Parse(source), IPv4Address.Parse(destination), timeToLive: ttl);

    [Fact]
    public void Create_StaticRoute_CarriesAStableId_AndDefaultRouteIsRecognised()
    {
        var id = Guid.NewGuid();
        var route = Route.Create(
            IPv4Network.Parse("0.0.0.0/0"), RouteType.Default, nextHop: IPv4Address.Parse("10.0.0.2"), id: id);

        Assert.Equal(id, route.Id);
        Assert.True(route.IsDefault);
        Assert.Equal(RouteType.Default, route.Type);
        Assert.Equal(1, route.AdministrativeDistance);
    }

    [Fact]
    public void Create_ConnectedRoute_HasEmptyId()
    {
        var lab = RoutingTestLab.WithOneInterface("192.168.1.1/24");
        Assert.Equal(Guid.Empty, lab.Router.RoutingTable.GetRoutes().Single().Id);
    }

    [Fact]
    public void StaticRoute_ToARemoteNetwork_IsSelectedAndResolvedThroughAConnectedRoute()
    {
        var lab = RoutingTestLab.WithTwoInterfaces("192.168.1.1/24", "10.0.0.1/30");
        lab.Router.RoutingTable.AddRoute(Route.Create(
            IPv4Network.Parse("192.168.2.0/24"), RouteType.Static, nextHop: IPv4Address.Parse("10.0.0.2")));

        var lookup = lab.Router.RoutingTable.FindBestRoute(IPv4Address.Parse("192.168.2.10"));

        Assert.True(lookup.HasRoute);
        Assert.True(lookup.IsResolvable);
        Assert.Equal(RouteType.Static, lookup.RouteType);
        Assert.Same(lab.Gi1, lookup.OutgoingInterface);
        Assert.Equal(IPv4Address.Parse("10.0.0.2"), lookup.NextHopAddress);
    }

    [Fact]
    public void MoreSpecificStaticRoute_BeatsTheDefaultStaticRoute()
    {
        var lab = RoutingTestLab.WithTwoInterfaces("192.168.1.1/24", "10.0.0.1/30");
        lab.Router.RoutingTable.AddRoute(Route.Create(
            IPv4Network.Parse("0.0.0.0/0"), RouteType.Default, nextHop: IPv4Address.Parse("10.0.0.2")));
        lab.Router.RoutingTable.AddRoute(Route.Create(
            IPv4Network.Parse("172.16.0.0/16"), RouteType.Static, nextHop: IPv4Address.Parse("10.0.0.2")));

        Assert.Equal(IPv4Network.Parse("172.16.0.0/16"),
            lab.Router.RoutingTable.FindBestRoute(IPv4Address.Parse("172.16.50.10")).Route!.Destination);
        Assert.Equal(IPv4Network.Parse("0.0.0.0/0"),
            lab.Router.RoutingTable.FindBestRoute(IPv4Address.Parse("8.8.8.8")).Route!.Destination);
    }

    [Fact]
    public void StaticRoute_DoesNotOverrideAMoreSpecificConnectedRoute()
    {
        var lab = RoutingTestLab.WithTwoInterfaces("192.168.1.1/24", "10.0.0.1/30");
        // A broad static route that also contains the directly connected /24.
        lab.Router.RoutingTable.AddRoute(Route.Create(
            IPv4Network.Parse("192.168.0.0/16"), RouteType.Static, nextHop: IPv4Address.Parse("10.0.0.2")));

        var lookup = lab.Router.RoutingTable.FindBestRoute(IPv4Address.Parse("192.168.1.50"));

        Assert.Equal(RouteType.Connected, lookup.RouteType);
        Assert.Same(lab.Gi0, lookup.OutgoingInterface);
    }

    [Fact]
    public void MatchedStaticRoute_WithADownTransitPath_IsHasRoute_ButNotResolvable()
    {
        var lab = RoutingTestLab.WithTwoInterfaces("192.168.1.1/24", "10.0.0.1/30");
        lab.Router.RoutingTable.AddRoute(Route.Create(
            IPv4Network.Parse("192.168.2.0/24"), RouteType.Static, nextHop: IPv4Address.Parse("10.0.0.2")));
        lab.Gi1.BringDown();

        var lookup = lab.Router.RoutingTable.FindBestRoute(IPv4Address.Parse("192.168.2.10"));

        Assert.True(lookup.HasRoute);
        Assert.False(lookup.IsResolvable);
        Assert.Contains("unusable", lookup.Reason);
    }

    [Fact]
    public void FindBestRoute_FallsBackToALessSpecificUsableRoute_WhenTheBestMatchCannotBeResolved()
    {
        var lab = RoutingTestLab.WithTwoInterfaces("192.168.1.1/24", "10.0.0.1/30");
        // The most specific route points at a next hop nothing can reach; a shorter route is fine.
        lab.Router.RoutingTable.AddRoute(Route.Create(
            IPv4Network.Parse("10.10.20.0/24"), RouteType.Static, nextHop: IPv4Address.Parse("172.31.99.1")));
        lab.Router.RoutingTable.AddRoute(Route.Create(
            IPv4Network.Parse("10.10.0.0/16"), RouteType.Static, nextHop: IPv4Address.Parse("10.0.0.2")));

        var lookup = lab.Router.RoutingTable.FindBestRoute(IPv4Address.Parse("10.10.20.5"));

        Assert.True(lookup.IsResolvable);
        Assert.Equal(IPv4Network.Parse("10.10.0.0/16"), lookup.Route!.Destination);
        Assert.Same(lab.Gi1, lookup.OutgoingInterface);
        Assert.Equal(IPv4Address.Parse("10.0.0.2"), lookup.NextHopAddress);
    }

    [Fact]
    public void RoutingEngine_ForwardsViaAStaticRoute_ArpingForTheNextHopNotTheDestination()
    {
        var lab = RoutingTestLab.WithTwoInterfaces("192.168.1.1/24", "10.0.0.1/30");
        lab.Router.RoutingTable.AddRoute(Route.Create(
            IPv4Network.Parse("192.168.2.0/24"), RouteType.Static, nextHop: IPv4Address.Parse("10.0.0.2")));
        var engine = new RoutingEngine();

        var result = engine.Route(lab.Router, lab.Gi0, Packet("192.168.1.10", "192.168.2.10", ttl: 64));

        Assert.Equal(RoutingOutcome.Forward, result.Outcome);
        Assert.Same(lab.Gi1, result.OutgoingInterface);
        Assert.Equal(IPv4Address.Parse("10.0.0.2"), result.NextHop);
        Assert.Equal((byte)63, result.NewTimeToLive);
        Assert.Equal(IPv4Address.Parse("192.168.2.10"), result.ForwardedPacket!.DestinationAddress);
    }

    [Fact]
    public void RoutingEngine_TreatsAnUnresolvableStaticRouteAsNoRoute_WithAnExplanatoryReason()
    {
        var lab = RoutingTestLab.WithTwoInterfaces("192.168.1.1/24", "10.0.0.1/30");
        lab.Router.RoutingTable.AddRoute(Route.Create(
            IPv4Network.Parse("192.168.2.0/24"), RouteType.Static, nextHop: IPv4Address.Parse("10.0.0.2")));
        lab.Gi1.BringDown();
        var engine = new RoutingEngine();

        var result = engine.Route(lab.Router, lab.Gi0, Packet("192.168.1.10", "192.168.2.10"));

        Assert.Equal(RoutingOutcome.NoRoute, result.Outcome);
        Assert.Contains("192.168.2.0/24", result.Reason);
        Assert.Contains("unusable", result.Reason);
    }

    [Fact]
    public void AddRoute_RefusesToOverrideAConnectedRouteWithAStaticOne()
    {
        var lab = RoutingTestLab.WithOneInterface("192.168.1.1/24");

        Assert.Throws<DomainException>(() => lab.Router.RoutingTable.AddRoute(
            Route.Create(IPv4Network.Parse("192.168.1.0/24"), RouteType.Static, nextHop: IPv4Address.Parse("10.0.0.2"))));
    }

    [Fact]
    public void SyncConnectedRoutes_LeavesStaticRoutesInPlace()
    {
        var lab = RoutingTestLab.WithTwoInterfaces("192.168.1.1/24", "10.0.0.1/30");
        lab.Router.RoutingTable.AddRoute(Route.Create(
            IPv4Network.Parse("192.168.2.0/24"), RouteType.Static, nextHop: IPv4Address.Parse("10.0.0.2")));

        lab.Router.SyncConnectedRoutes();

        Assert.Contains(lab.Router.RoutingTable.GetRoutes(),
            r => r.Type == RouteType.Static && r.Destination == IPv4Network.Parse("192.168.2.0/24"));
    }
}
