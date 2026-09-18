using System.Linq;
using NetSim.Core.Common.Exceptions;
using NetSim.Core.Dhcp;
using NetSim.Core.Networking;

namespace NetSim.Core.Tests.Dhcp;

public class DhcpServerTests
{
    private static readonly MacAddress MacA = MacAddress.Parse("AA:BB:CC:00:00:0A");
    private static readonly MacAddress MacB = MacAddress.Parse("AA:BB:CC:00:00:0B");
    private static readonly IPv4Address ServerIp = IPv4Address.Parse("192.168.1.1");
    private static readonly IPv4Address DnsIp = IPv4Address.Parse("192.168.1.53");

    private static DhcpServer BuildServer(string start = "192.168.1.100", string end = "192.168.1.200", TimeProvider? time = null) =>
        new(new DhcpServerConfiguration(
            ServerIp, 24, IPv4Address.Parse(start), IPv4Address.Parse(end),
            defaultGateway: ServerIp, dnsServer: DnsIp, leaseDuration: TimeSpan.FromSeconds(3600)), time);

    private static DhcpMessage Discover(MacAddress mac, uint xid = 1) => DhcpMessage.CreateDiscover(xid, mac);

    private static DhcpMessage RequestFor(DhcpMessage offer) => DhcpMessage.CreateRequest(
        offer.TransactionId, offer.ClientHardwareAddress,
        new DhcpOptions.Builder { RequestedIpAddress = offer.YourIpAddress, ServerIdentifier = offer.Options.ServerIdentifier }.Build());

    [Fact]
    public void Constructor_RejectsAnInvalidConfiguration()
    {
        var bad = new DhcpServerConfiguration(ServerIp, 24, IPv4Address.Parse("192.168.1.200"), IPv4Address.Parse("192.168.1.100"));

        Assert.Throws<DomainException>(() => new DhcpServer(bad));
    }

    [Fact]
    public void Discover_ProducesAnOfferWithAFullConfiguration()
    {
        var server = BuildServer();
        var offerRaised = false;
        server.OfferCreated += (_, _) => offerRaised = true;

        var offer = server.HandleMessage(Discover(MacA, xid: 0x42));

        Assert.NotNull(offer);
        Assert.Equal(DhcpMessageType.Offer, offer!.MessageType);
        Assert.Equal(0x42u, offer.TransactionId);
        Assert.Equal(IPv4Address.Parse("192.168.1.100"), offer.YourIpAddress);
        Assert.Equal(SubnetMask.Parse("255.255.255.0"), offer.Options.SubnetMask);
        Assert.Equal(ServerIp, offer.Options.Router);
        Assert.Equal(DnsIp, offer.Options.PrimaryDnsServer);
        Assert.Equal(ServerIp, offer.Options.ServerIdentifier);
        Assert.Equal(TimeSpan.FromSeconds(3600), offer.Options.IpAddressLeaseTime);
        Assert.True(offerRaised);
    }

    [Fact]
    public void Request_ForTheOfferedAddress_ProducesAnAck_AndAnActiveLease()
    {
        var server = BuildServer();
        var offer = server.HandleMessage(Discover(MacA))!;

        var ack = server.HandleMessage(RequestFor(offer));

        Assert.NotNull(ack);
        Assert.Equal(DhcpMessageType.Ack, ack!.MessageType);
        Assert.Equal(offer.YourIpAddress, ack.YourIpAddress);
        Assert.Contains(server.Leases.ActiveLeases, l => l.Address == offer.YourIpAddress);
    }

    [Fact]
    public void Request_ForAnAddressOutsideThePool_ProducesANak()
    {
        var server = BuildServer();
        server.HandleMessage(Discover(MacA));
        var nakRaised = false;
        server.NakCreated += (_, _) => nakRaised = true;

        var request = DhcpMessage.CreateRequest(1, MacA, new DhcpOptions.Builder
        {
            RequestedIpAddress = IPv4Address.Parse("192.168.1.250"),
            ServerIdentifier = ServerIp,
        }.Build());
        var reply = server.HandleMessage(request);

        Assert.Equal(DhcpMessageType.Nak, reply!.MessageType);
        Assert.True(nakRaised);
        Assert.Empty(server.Leases.ActiveLeases);
    }

    [Fact]
    public void Request_NamingADifferentServer_IsIgnored_AndTheOfferIsCancelled()
    {
        var server = BuildServer();
        var offer = server.HandleMessage(Discover(MacA))!;

        var request = DhcpMessage.CreateRequest(offer.TransactionId, MacA, new DhcpOptions.Builder
        {
            RequestedIpAddress = offer.YourIpAddress,
            ServerIdentifier = IPv4Address.Parse("10.9.9.9"), // a different server
        }.Build());
        var reply = server.HandleMessage(request);

        Assert.Null(reply);
        Assert.DoesNotContain(server.Leases.Leases, l => l.State == DhcpLeaseState.Offered);
    }

    [Fact]
    public void Release_ReturnsTheAddressToThePool()
    {
        var server = BuildServer();
        var offer = server.HandleMessage(Discover(MacA))!;
        server.HandleMessage(RequestFor(offer));
        var released = false;
        server.LeaseReleased += (_, _) => released = true;

        var reply = server.HandleMessage(DhcpMessage.CreateRelease(2, MacA, offer.YourIpAddress, ServerIp));

        Assert.Null(reply);
        Assert.True(released);
        Assert.Empty(server.Leases.ActiveLeases);
    }

    [Fact]
    public void Decline_MarksTheAddressUnusable()
    {
        var server = BuildServer("192.168.1.100", "192.168.1.101");
        var offer = server.HandleMessage(Discover(MacA))!;
        server.HandleMessage(RequestFor(offer));
        var declinedRaised = false;
        server.AddressDeclined += (_, _) => declinedRaised = true;

        server.HandleMessage(DhcpMessage.CreateDecline(3, MacA, offer.YourIpAddress, ServerIp));

        Assert.True(declinedRaised);
        // The next client must not be handed the declined address.
        var nextOffer = server.HandleMessage(Discover(MacB));
        Assert.NotEqual(offer.YourIpAddress, nextOffer!.YourIpAddress);
    }

    [Fact]
    public void PoolExhaustion_RaisesTheEvent_AndReturnsNoOffer()
    {
        var server = BuildServer("192.168.1.100", "192.168.1.100"); // one address
        server.HandleMessage(RequestFor(server.HandleMessage(Discover(MacA))!));
        var exhaustedRaised = false;
        server.PoolExhausted += (_, _) => exhaustedRaised = true;

        var offer = server.HandleMessage(Discover(MacB));

        Assert.Null(offer);
        Assert.True(exhaustedRaised);
    }

    [Fact]
    public void MultipleClients_EachGetTheirOwnLease()
    {
        var server = BuildServer();

        var offerA = server.HandleMessage(Discover(MacA, xid: 1))!;
        server.HandleMessage(RequestFor(offerA));
        var offerB = server.HandleMessage(Discover(MacB, xid: 2))!;
        server.HandleMessage(RequestFor(offerB));

        Assert.Equal(2, server.Leases.ActiveLeases.Count);
        Assert.NotEqual(offerA.YourIpAddress, offerB.YourIpAddress);
    }

    [Fact]
    public void PruneExpiredLeases_RaisesLeaseExpired_WhenALeaseRunsOut()
    {
        var time = TestTimeProvider.StartingAtEpoch();
        var server = BuildServer(time: time);
        var offer = server.HandleMessage(Discover(MacA))!;
        server.HandleMessage(RequestFor(offer));
        var expiredRaised = false;
        server.LeaseExpired += (_, _) => expiredRaised = true;

        time.Advance(TimeSpan.FromSeconds(3601));
        var expired = server.PruneExpiredLeases();

        Assert.Single(expired);
        Assert.True(expiredRaised);
    }
}
