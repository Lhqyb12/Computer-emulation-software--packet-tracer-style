using System;
using System.Linq;
using NetSim.Application.Common;
using NetSim.Application.Projects;
using NetSim.Application.Routing;
using NetSim.Application.State;
using NetSim.Core.Common;
using NetSim.Core.Devices;
using NetSim.Core.Networking;
using NetSim.Core.Routing;
using NetSim.Core.Topology;

namespace NetSim.Application.Tests.Routing;

/// <summary>
/// Phase 28 sections 17-20, 24, 36: the static-route management API. Add / edit / remove /
/// validate a router's static and default routes, reject invalid or duplicate configuration,
/// never touch connected routes, and report a meaningful status.
/// </summary>
public class StaticRouteServiceTests
{
    private sealed record Lab(StaticRouteService Service, ApplicationState State, Router Router, NetworkInterface Gi0, NetworkInterface Gi1);

    private static Lab Build()
    {
        var state = new ApplicationState();
        state.SetCurrentProject(new Project(EntityId.New(), "Test", null, DateTime.UtcNow, DateTime.UtcNow, null, 1));
        var network = new Network("Lab");
        state.SetCurrentNetwork(network);

        var router = (Router)NetworkDeviceFactory.Create(DeviceType.Router, "R1");
        network.AddDevice(router);
        var gi0 = router.Interfaces.First(i => i.Name == "GigabitEthernet0/0");
        var gi1 = router.Interfaces.First(i => i.Name == "GigabitEthernet0/1");
        gi0.BringUp();
        gi1.BringUp();
        gi0.SetPrimaryIPv4Configuration(Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("192.168.1.1"), 24));
        gi1.SetPrimaryIPv4Configuration(Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("10.0.0.1"), 30));
        router.SyncConnectedRoutes();

        return new Lab(new StaticRouteService(state), state, router, gi0, gi1);
    }

    private static StaticRouteInput Input(string dest, int prefix, string? nextHop = null, string? iface = null) =>
        new() { DestinationNetwork = dest, PrefixLength = prefix, NextHop = nextHop, OutgoingInterfaceName = iface };

    [Fact]
    public void AddStaticRoute_NextHopRoute_IsAddedAndActive_AndMarksProjectDirty()
    {
        var lab = Build();
        lab.State.MarkCurrentProjectClean();
        var changed = 0;
        lab.Service.StaticRoutesChanged += (_, _) => changed++;

        var result = lab.Service.AddStaticRoute(lab.Router, Input("192.168.2.0", 24, nextHop: "10.0.0.2"));

        Assert.True(result.IsSuccess);
        Assert.Equal(StaticRouteStatus.Active, result.Value!.Status);
        Assert.Equal("Static", result.Value.RouteTypeText);
        Assert.True(lab.State.IsCurrentProjectDirty);
        Assert.Equal(1, changed);

        var route = lab.Router.RoutingTable.GetRoutes().Single(r => r.Type == RouteType.Static);
        Assert.Equal(IPv4Network.Parse("192.168.2.0/24"), route.Destination);
        Assert.Equal(IPv4Address.Parse("10.0.0.2"), route.NextHop);
    }

    [Fact]
    public void AddStaticRoute_DefaultRoute_IsTypedAsDefault()
    {
        var lab = Build();

        var result = lab.Service.AddStaticRoute(lab.Router, Input("0.0.0.0", 0, nextHop: "10.0.0.2"));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsDefaultRoute);
        Assert.Equal("Default", result.Value.RouteTypeText);
        Assert.Equal(RouteType.Default, lab.Router.RoutingTable.GetRoutes().Single(r => r.IsDefault).Type);
    }

    [Fact]
    public void AddStaticRoute_ExitInterfaceRoute_ResolvesToThatInterface()
    {
        var lab = Build();

        var result = lab.Service.AddStaticRoute(lab.Router, Input("192.168.9.0", 24, iface: "G0/1"));

        Assert.True(result.IsSuccess);
        Assert.Equal("G0/1", result.Value!.OutgoingInterfaceName);
        Assert.Equal(StaticRouteStatus.Active, result.Value.Status);
    }

    [Theory]
    [InlineData("not-an-ip", 24, null, null, "destination")]
    [InlineData("192.168.2.0", 40, "10.0.0.2", null, "Prefix")]
    [InlineData("192.168.2.0", 24, null, null, "needs a next hop")]
    [InlineData("192.168.2.0", 24, "999.1.1.1", null, "valid IPv4")]
    [InlineData("192.168.2.0", 24, "255.255.255.255", null, "usable next-hop")]
    [InlineData("192.168.2.0", 24, "10.0.0.2", "G9/9", "does not exist")]
    [InlineData("192.168.2.0", 24, "192.168.1.1", null, "own interface")]
    public void AddStaticRoute_RejectsInvalidInput_WithoutTouchingTheTable(
        string dest, int prefix, string? nextHop, string? iface, string expectedFragment)
    {
        var lab = Build();
        var before = lab.Router.RoutingTable.GetRoutes().Count;

        var result = lab.Service.AddStaticRoute(lab.Router, Input(dest, prefix, nextHop, iface));

        Assert.False(result.IsSuccess);
        Assert.Equal(OperationErrorType.ValidationError, result.ErrorType);
        Assert.Contains(expectedFragment, result.ErrorMessage);
        Assert.Equal(before, lab.Router.RoutingTable.GetRoutes().Count);
    }

    [Fact]
    public void AddStaticRoute_RejectsAFullySpecifiedRouteWhoseNextHopIsNotOnTheStatedInterface()
    {
        var lab = Build();

        var result = lab.Service.AddStaticRoute(lab.Router, Input("172.16.0.0", 16, nextHop: "192.168.50.1", iface: "G0/1"));

        Assert.False(result.IsSuccess);
        Assert.Contains("not on interface", result.ErrorMessage);
    }

    [Fact]
    public void AddStaticRoute_RejectsADuplicatePrefix()
    {
        var lab = Build();
        lab.Service.AddStaticRoute(lab.Router, Input("192.168.2.0", 24, nextHop: "10.0.0.2"));

        var again = lab.Service.AddStaticRoute(lab.Router, Input("192.168.2.0", 24, nextHop: "10.0.0.3"));

        Assert.False(again.IsSuccess);
        Assert.Contains("already exists", again.ErrorMessage);
        Assert.Single(lab.Router.RoutingTable.GetRoutes(), r => r.Type == RouteType.Static);
    }

    [Fact]
    public void AddStaticRoute_RejectsAPrefixThatIsADirectlyConnectedNetwork()
    {
        var lab = Build();

        var result = lab.Service.AddStaticRoute(lab.Router, Input("192.168.1.0", 24, nextHop: "10.0.0.2"));

        Assert.False(result.IsSuccess);
        Assert.Contains("directly connected", result.ErrorMessage);
    }

    [Fact]
    public void UpdateStaticRoute_ChangesConfiguration_KeepingTheSameIdentity()
    {
        var lab = Build();
        var added = lab.Service.AddStaticRoute(lab.Router, Input("192.168.2.0", 24, nextHop: "10.0.0.2")).Value!;

        var updated = lab.Service.UpdateStaticRoute(lab.Router, added.Id, Input("192.168.3.0", 24, nextHop: "10.0.0.2"));

        Assert.True(updated.IsSuccess);
        Assert.Equal(added.Id, updated.Value!.Id);
        Assert.Equal("192.168.3.0", updated.Value.DestinationNetwork);
        Assert.DoesNotContain(lab.Router.RoutingTable.GetRoutes(),
            r => r.Destination == IPv4Network.Parse("192.168.2.0/24"));
        Assert.Single(lab.Router.RoutingTable.GetRoutes(), r => r.Type == RouteType.Static);
    }

    [Fact]
    public void UpdateStaticRoute_InvalidEdit_LeavesTheOriginalRouteInPlace()
    {
        var lab = Build();
        var added = lab.Service.AddStaticRoute(lab.Router, Input("192.168.2.0", 24, nextHop: "10.0.0.2")).Value!;

        var updated = lab.Service.UpdateStaticRoute(lab.Router, added.Id, Input("bad", 24, nextHop: "10.0.0.2"));

        Assert.False(updated.IsSuccess);
        Assert.Contains(lab.Router.RoutingTable.GetRoutes(),
            r => r.Id == added.Id && r.Destination == IPv4Network.Parse("192.168.2.0/24"));
    }

    [Fact]
    public void UpdateStaticRoute_UnknownId_ReturnsNotFound()
    {
        var lab = Build();
        var result = lab.Service.UpdateStaticRoute(lab.Router, System.Guid.NewGuid(), Input("192.168.2.0", 24, nextHop: "10.0.0.2"));
        Assert.Equal(OperationErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public void RemoveStaticRoute_RemovesItFromTheLookup()
    {
        var lab = Build();
        var added = lab.Service.AddStaticRoute(lab.Router, Input("192.168.2.0", 24, nextHop: "10.0.0.2")).Value!;

        var removed = lab.Service.RemoveStaticRoute(lab.Router, added.Id);

        Assert.True(removed.IsSuccess);
        Assert.Empty(lab.Service.GetStaticRoutes(lab.Router));
        Assert.False(lab.Router.RoutingTable.FindBestRoute(IPv4Address.Parse("192.168.2.10")).HasRoute);
    }

    [Fact]
    public void RemoveStaticRoute_CannotRemoveAConnectedRoute()
    {
        var lab = Build();
        // A connected route's id is Guid.Empty and it is not a "managed" route.
        var result = lab.Service.RemoveStaticRoute(lab.Router, System.Guid.Empty);
        Assert.Equal(OperationErrorType.NotFound, result.ErrorType);
        Assert.Equal(2, lab.Router.RoutingTable.GetRoutes().Count(r => r.Type == RouteType.Connected));
    }

    [Fact]
    public void GetStaticRoutes_ReportsInterfaceDown_ThenRecoversWhenTheInterfaceReturns()
    {
        var lab = Build();
        lab.Service.AddStaticRoute(lab.Router, Input("192.168.2.0", 24, nextHop: "10.0.0.2"));

        lab.Gi1.BringDown();
        Assert.Equal(StaticRouteStatus.UnreachableNextHop, lab.Service.GetStaticRoutes(lab.Router).Single().Status);

        lab.Gi1.BringUp();
        Assert.Equal(StaticRouteStatus.Active, lab.Service.GetStaticRoutes(lab.Router).Single().Status);

        // The route was never deleted.
        Assert.Single(lab.Service.GetStaticRoutes(lab.Router));
    }

    [Fact]
    public void ValidateStaticRoute_ReportsWithoutMutating()
    {
        var lab = Build();

        Assert.True(lab.Service.ValidateStaticRoute(lab.Router, Input("192.168.2.0", 24, nextHop: "10.0.0.2")).IsValid);
        Assert.False(lab.Service.ValidateStaticRoute(lab.Router, Input("192.168.2.0", 33, nextHop: "10.0.0.2")).IsValid);
        Assert.DoesNotContain(lab.Router.RoutingTable.GetRoutes(), r => r.Type is RouteType.Static or RouteType.Default);
    }
}
