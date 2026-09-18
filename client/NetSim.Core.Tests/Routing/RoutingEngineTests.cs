using NetSim.Core.IP;
using NetSim.Core.Icmp;
using NetSim.Core.Networking;
using NetSim.Core.Routing;

namespace NetSim.Core.Tests.Routing;

/// <summary>
/// Phase 27 sections 16-20, 32, 34-35, 70: the Layer 3 forwarding decision. Local delivery vs
/// forwarding, longest-prefix match, TTL decrement / expiry, no-route, broadcast drop, and the
/// structured events a future timeline needs.
/// </summary>
public class RoutingEngineTests
{
    private static IPv4Packet Packet(string source, string destination, byte ttl = 64) =>
        IPv4Packet.Create(IPv4Address.Parse(source), IPv4Address.Parse(destination), timeToLive: ttl);

    [Fact]
    public void PacketAddressedToARouterInterface_IsDeliveredLocally_NeverForwarded()
    {
        var lab = RoutingTestLab.WithTwoInterfaces("192.168.1.1/24", "192.168.2.1/24");
        var engine = new RoutingEngine();

        var result = engine.Route(lab.Router, lab.Gi0, Packet("192.168.1.10", "192.168.1.1"));

        Assert.Equal(RoutingOutcome.LocalDelivery, result.Outcome);
        Assert.Null(result.ForwardedPacket);
        Assert.Null(result.OutgoingInterface);
    }

    [Fact]
    public void PacketForAnotherConnectedNetwork_IsForwardedOutTheRightInterface_WithTtlDecremented()
    {
        var lab = RoutingTestLab.WithTwoInterfaces("192.168.1.1/24", "192.168.2.1/24");
        var engine = new RoutingEngine();

        var original = Packet("192.168.1.10", "192.168.2.10", ttl: 64);
        var result = engine.Route(lab.Router, lab.Gi0, original);

        Assert.Equal(RoutingOutcome.Forward, result.Outcome);
        Assert.Same(lab.Gi1, result.OutgoingInterface);
        Assert.Equal(IPv4Address.Parse("192.168.2.10"), result.NextHop);         // directly connected -> dest is the L2 next hop
        Assert.Equal(RouteType.Connected, result.Route!.Type);
        Assert.Equal((byte)64, result.OriginalTimeToLive);
        Assert.Equal((byte)63, result.NewTimeToLive);
        Assert.Equal(IPv4Address.Parse("192.168.1.10"), result.ForwardedPacket!.SourceAddress);
        Assert.Equal(IPv4Address.Parse("192.168.2.10"), result.ForwardedPacket.DestinationAddress);
    }

    [Fact]
    public void Forwarding_UpdatesTheIPv4HeaderChecksum_AndBothPacketsStayValid()
    {
        var lab = RoutingTestLab.WithTwoInterfaces("192.168.1.1/24", "192.168.2.1/24");
        var engine = new RoutingEngine();

        var original = Packet("192.168.1.10", "192.168.2.10", ttl: 64);
        var result = engine.Route(lab.Router, lab.Gi0, original);

        Assert.True(original.Validate().IsValid);
        Assert.True(result.ForwardedPacket!.Validate().IsValid);
        // The checksum is derived from the header, so decrementing the TTL changes it.
        Assert.NotEqual(original.Header.ComputeChecksum(), result.ForwardedPacket.Header.ComputeChecksum());
    }

    [Fact]
    public void NoRoute_DropsThePacket_AndSuggestsIcmpNetworkUnreachable()
    {
        var lab = RoutingTestLab.WithTwoInterfaces("192.168.1.1/24", "192.168.2.1/24");
        var engine = new RoutingEngine();

        var result = engine.Route(lab.Router, lab.Gi0, Packet("192.168.1.10", "10.10.10.10"));

        Assert.Equal(RoutingOutcome.NoRoute, result.Outcome);
        Assert.Null(result.ForwardedPacket);
        Assert.Equal(IcmpType.DestinationUnreachable, result.SuggestedIcmpError);
        Assert.Equal(IcmpCode.NetworkUnreachable, result.SuggestedIcmpCode);
    }

    [Fact]
    public void TtlOne_CannotSurviveTheDecrement_TimeExceeded()
    {
        var lab = RoutingTestLab.WithTwoInterfaces("192.168.1.1/24", "192.168.2.1/24");
        var engine = new RoutingEngine();

        var result = engine.Route(lab.Router, lab.Gi0, Packet("192.168.1.10", "192.168.2.10", ttl: 1));

        Assert.Equal(RoutingOutcome.TtlExpired, result.Outcome);
        Assert.Null(result.ForwardedPacket);
        Assert.Equal(IcmpType.TimeExceeded, result.SuggestedIcmpError);
        Assert.Equal(IcmpCode.TimeToLiveExceededInTransit, result.SuggestedIcmpCode);
    }

    [Fact]
    public void IngressInterfaceDown_PacketIsNotProcessed()
    {
        var lab = RoutingTestLab.WithTwoInterfaces("192.168.1.1/24", "192.168.2.1/24");
        lab.Gi0.BringDown();
        var engine = new RoutingEngine();

        var result = engine.Route(lab.Router, lab.Gi0, Packet("192.168.1.10", "192.168.2.10"));

        Assert.Equal(RoutingOutcome.InterfaceDown, result.Outcome);
    }

    [Fact]
    public void ConnectedRouteForADownInterface_IsInactive_SoTheDestinationHasNoRoute()
    {
        var lab = RoutingTestLab.WithTwoInterfaces("192.168.1.1/24", "192.168.2.1/24");
        lab.Gi1.BringDown();
        var engine = new RoutingEngine();

        var result = engine.Route(lab.Router, lab.Gi0, Packet("192.168.1.10", "192.168.2.10"));

        Assert.Equal(RoutingOutcome.NoRoute, result.Outcome);
    }

    [Fact]
    public void BroadcastDestination_IsNotBridgedByTheRouter()
    {
        var lab = RoutingTestLab.WithTwoInterfaces("192.168.1.1/24", "192.168.2.1/24");
        var engine = new RoutingEngine();

        var result = engine.Route(lab.Router, lab.Gi0, Packet("192.168.1.10", "255.255.255.255"));

        Assert.Equal(RoutingOutcome.Drop, result.Outcome);
    }

    [Fact]
    public void DefaultRoute_IsUsedWhenNoConnectedNetworkMatches()
    {
        var lab = RoutingTestLab.WithTwoInterfaces("192.168.1.1/24", "10.0.0.1/30");
        // A (Phase-28-style) static default route via the peer on the /30 link.
        lab.Router.RoutingTable.AddRoute(Route.Create(
            IPv4Network.Parse("0.0.0.0/0"), RouteType.Default, lab.Gi1, nextHop: IPv4Address.Parse("10.0.0.2")));
        var engine = new RoutingEngine();

        var result = engine.Route(lab.Router, lab.Gi0, Packet("192.168.1.10", "8.8.8.8"));

        Assert.Equal(RoutingOutcome.Forward, result.Outcome);
        Assert.Same(lab.Gi1, result.OutgoingInterface);
        Assert.Equal(IPv4Address.Parse("10.0.0.2"), result.NextHop);   // ARP resolves the next-hop router, not 8.8.8.8
    }

    [Fact]
    public void Route_RaisesTheForwardingEvents()
    {
        var lab = RoutingTestLab.WithTwoInterfaces("192.168.1.1/24", "192.168.2.1/24");
        var engine = new RoutingEngine();
        var received = 0;
        var selected = 0;
        var forwarded = 0;
        engine.PacketReceived += (_, _) => received++;
        engine.RouteSelected += (_, _) => selected++;
        engine.PacketForwarded += (_, _) => forwarded++;

        engine.Route(lab.Router, lab.Gi0, Packet("192.168.1.10", "192.168.2.10"));

        Assert.Equal(1, received);
        Assert.Equal(1, selected);
        Assert.Equal(1, forwarded);
    }
}
