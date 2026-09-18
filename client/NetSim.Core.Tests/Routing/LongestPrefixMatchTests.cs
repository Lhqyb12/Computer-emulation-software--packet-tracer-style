using System.Collections.Generic;
using NetSim.Core.Networking;
using NetSim.Core.Routing;

namespace NetSim.Core.Tests.Routing;

/// <summary>
/// Phase 27 section 12/67: the pure route-selection algorithm. Longest prefix wins; a default
/// route is the fallback; administrative distance / metric break a prefix-length tie; the result
/// never depends on insertion order.
/// </summary>
public class LongestPrefixMatchTests
{
    private static Route StaticRoute(string cidr, string nextHop, int metric = 0, int? adminDistance = null) =>
        Route.Create(
            IPv4Network.Parse(cidr), RouteType.Static, outgoingInterface: null,
            nextHop: IPv4Address.Parse(nextHop), metric: metric, administrativeDistance: adminDistance);

    [Theory]
    [InlineData("10.10.20.50", "10.10.20.0/24")]
    [InlineData("10.10.30.50", "10.10.0.0/16")]
    [InlineData("10.50.1.1", "10.0.0.0/8")]
    public void Select_ChoosesTheLongestMatchingPrefix(string destination, string expectedNetwork)
    {
        var routes = new List<Route>
        {
            StaticRoute("10.0.0.0/8", "192.0.2.1"),
            StaticRoute("10.10.0.0/16", "192.0.2.2"),
            StaticRoute("10.10.20.0/24", "192.0.2.3"),
        };

        var best = LongestPrefixMatch.Select(routes, IPv4Address.Parse(destination));

        Assert.NotNull(best);
        Assert.Equal(IPv4Network.Parse(expectedNetwork), best!.Destination);
    }

    [Fact]
    public void Select_IsIndependentOfInsertionOrder()
    {
        var forward = new List<Route>
        {
            StaticRoute("10.10.20.0/24", "192.0.2.3"),
            StaticRoute("10.0.0.0/8", "192.0.2.1"),
            StaticRoute("10.10.0.0/16", "192.0.2.2"),
        };
        var reversed = new List<Route>(forward);
        reversed.Reverse();

        var dest = IPv4Address.Parse("10.10.20.99");
        Assert.Equal(
            LongestPrefixMatch.Select(forward, dest)!.Destination,
            LongestPrefixMatch.Select(reversed, dest)!.Destination);
    }

    [Fact]
    public void Select_UsesDefaultRouteOnlyWhenNoMoreSpecificRouteMatches()
    {
        var routes = new List<Route>
        {
            StaticRoute("192.168.1.0/24", "192.0.2.1"),
            StaticRoute("0.0.0.0/0", "192.0.2.254"),
        };

        Assert.Equal(IPv4Network.Parse("0.0.0.0/0"),
            LongestPrefixMatch.Select(routes, IPv4Address.Parse("8.8.8.8"))!.Destination);
        Assert.Equal(IPv4Network.Parse("192.168.1.0/24"),
            LongestPrefixMatch.Select(routes, IPv4Address.Parse("192.168.1.5"))!.Destination);
    }

    [Fact]
    public void Select_MoreSpecificRouteBeatsDefaultRoute()
    {
        var routes = new List<Route>
        {
            StaticRoute("0.0.0.0/0", "192.0.2.254"),
            StaticRoute("8.8.8.0/24", "192.0.2.1"),
        };

        Assert.Equal(IPv4Network.Parse("8.8.8.0/24"),
            LongestPrefixMatch.Select(routes, IPv4Address.Parse("8.8.8.8"))!.Destination);
    }

    [Fact]
    public void Select_ReturnsNull_WhenNothingMatchesAndThereIsNoDefaultRoute()
    {
        var routes = new List<Route>
        {
            StaticRoute("192.168.1.0/24", "192.0.2.1"),
            StaticRoute("192.168.2.0/24", "192.0.2.2"),
        };

        Assert.Null(LongestPrefixMatch.Select(routes, IPv4Address.Parse("10.10.10.10")));
    }

    [Fact]
    public void Select_TieBrokenByAdministrativeDistanceThenMetric()
    {
        var lowDistance = StaticRoute("172.16.0.0/16", "192.0.2.1", metric: 50, adminDistance: 1);
        var highDistance = StaticRoute("172.16.0.0/16", "192.0.2.2", metric: 1, adminDistance: 200);

        var best = LongestPrefixMatch.Select(new[] { highDistance, lowDistance }, IPv4Address.Parse("172.16.5.5"));

        Assert.Same(lowDistance, best);
    }

    [Fact]
    public void Select_SkipsInactiveRoutes()
    {
        // An inactive route (down interface) must never be selected even though it is the longest match.
        var lab = RoutingTestLab.WithTwoInterfaces();
        var connectedRoute = Route.Connected(lab.Gi0, IPv4Network.Parse("192.168.1.0/24"));
        lab.Gi0.BringDown();

        var fallback = StaticRoute("0.0.0.0/0", "10.0.0.1");

        var best = LongestPrefixMatch.Select(new[] { connectedRoute, fallback }, IPv4Address.Parse("192.168.1.10"));

        Assert.Same(fallback, best);
    }
}
