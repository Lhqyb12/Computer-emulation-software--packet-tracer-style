using System.Linq;
using NetSim.Core.Dhcp;
using NetSim.Core.Networking;

namespace NetSim.Core.Tests.Dhcp;

public class DhcpLeaseManagerTests
{
    private static readonly IPv4Network Subnet = IPv4Network.Create(IPv4Address.Parse("192.168.1.0"), 24);
    private static readonly IPv4Address Server = IPv4Address.Parse("192.168.1.1");
    private static readonly DhcpClientId ClientA = DhcpClientId.FromHardwareAddress(MacAddress.Parse("AA:BB:CC:00:00:0A"));
    private static readonly DhcpClientId ClientB = DhcpClientId.FromHardwareAddress(MacAddress.Parse("AA:BB:CC:00:00:0B"));
    private static readonly DhcpClientId ClientC = DhcpClientId.FromHardwareAddress(MacAddress.Parse("AA:BB:CC:00:00:0C"));

    private static (DhcpLeaseManager Manager, TestTimeProvider Time) Build(string start = "192.168.1.100", string end = "192.168.1.110")
    {
        var time = TestTimeProvider.StartingAtEpoch();
        var pool = new DhcpAddressPool(IPv4Address.Parse(start), IPv4Address.Parse(end), Subnet, Server);
        return (new DhcpLeaseManager(pool, Server, TimeSpan.FromSeconds(3600), time), time);
    }

    [Fact]
    public void Offer_ReturnsTheLowestFreeAddress_AsAnOfferedLease()
    {
        var (manager, _) = Build();

        var lease = manager.Offer(ClientA, requestedAddress: null);

        Assert.NotNull(lease);
        Assert.Equal(IPv4Address.Parse("192.168.1.100"), lease!.Address);
        Assert.Equal(DhcpLeaseState.Offered, lease.State);
    }

    [Fact]
    public void Commit_TurnsAnOfferIntoAnActiveLease()
    {
        var (manager, _) = Build();
        var offer = manager.Offer(ClientA, null)!;

        var result = manager.Commit(ClientA, offer.Address);

        Assert.True(result.IsCommitted);
        Assert.Equal(DhcpLeaseState.Active, result.Lease!.State);
        Assert.Contains(manager.ActiveLeases, l => l.Address == offer.Address && l.ClientId == ClientA);
    }

    [Fact]
    public void TwoClients_NeverReceiveTheSameActiveAddress()
    {
        var (manager, _) = Build();

        var a = manager.Offer(ClientA, null)!;
        manager.Commit(ClientA, a.Address);
        var b = manager.Offer(ClientB, null)!;
        manager.Commit(ClientB, b.Address);

        Assert.NotEqual(a.Address, b.Address);
        Assert.Equal(2, manager.ActiveLeases.Count);
    }

    [Fact]
    public void Commit_ForAnAddressHeldByAnotherActiveClient_IsRejected()
    {
        var (manager, _) = Build();
        var a = manager.Offer(ClientA, null)!;
        manager.Commit(ClientA, a.Address);

        var result = manager.Commit(ClientB, a.Address);

        Assert.False(result.IsCommitted);
        Assert.Equal(DhcpCommitOutcome.HeldByAnotherClient, result.Outcome);
    }

    [Fact]
    public void Commit_ForAnAddressOutsideThePool_IsRejectedAsOutOfRange()
    {
        var (manager, _) = Build();

        var result = manager.Commit(ClientA, IPv4Address.Parse("192.168.1.250"));

        Assert.Equal(DhcpCommitOutcome.OutOfRange, result.Outcome);
    }

    [Fact]
    public void Renewal_ExtendsTheSameLease_KeepingTheAddress()
    {
        var (manager, time) = Build();
        var a = manager.Offer(ClientA, null)!;
        var committed = manager.Commit(ClientA, a.Address).Lease!;
        var firstExpiry = committed.Expiration;

        time.Advance(TimeSpan.FromSeconds(1800));
        var renewed = manager.Commit(ClientA, a.Address).Lease!;

        Assert.Equal(a.Address, renewed.Address);
        Assert.True(renewed.Expiration > firstExpiry);
        Assert.Single(manager.ActiveLeases);
    }

    [Fact]
    public void PruneExpired_MovesAnOverdueLeaseToExpired_AndFreesTheAddress()
    {
        var (manager, time) = Build();
        var a = manager.Offer(ClientA, null)!;
        manager.Commit(ClientA, a.Address);

        time.Advance(TimeSpan.FromSeconds(3601));
        var expired = manager.PruneExpired();

        Assert.Single(expired);
        Assert.Equal(DhcpLeaseState.Expired, expired[0].State);
        Assert.Empty(manager.ActiveLeases);
        Assert.False(manager.IsAddressInUse(a.Address));
    }

    [Fact]
    public void AnExpiredAddress_CanBeReusedByAnotherClient()
    {
        var (manager, time) = Build("192.168.1.100", "192.168.1.100"); // pool of one
        var a = manager.Offer(ClientA, null)!;
        manager.Commit(ClientA, a.Address);

        time.Advance(TimeSpan.FromSeconds(3601));
        manager.PruneExpired();

        var b = manager.Offer(ClientB, null);
        Assert.NotNull(b);
        Assert.Equal(a.Address, b!.Address);
    }

    [Fact]
    public void Release_ReturnsTheAddressToThePool()
    {
        var (manager, _) = Build("192.168.1.100", "192.168.1.100");
        var a = manager.Offer(ClientA, null)!;
        manager.Commit(ClientA, a.Address);

        Assert.True(manager.Release(ClientA));

        Assert.False(manager.IsAddressInUse(a.Address));
        var b = manager.Offer(ClientB, null);
        Assert.Equal(a.Address, b!.Address);
    }

    [Fact]
    public void Decline_HoldsTheAddressOutOfThePool()
    {
        var (manager, _) = Build("192.168.1.100", "192.168.1.101");
        var a = manager.Offer(ClientA, null)!;
        manager.Commit(ClientA, a.Address);
        manager.Decline(ClientA, a.Address);

        var next = manager.Offer(ClientA, null)!;

        Assert.NotEqual(a.Address, next.Address);
        Assert.True(manager.IsAddressInUse(a.Address));
    }

    [Fact]
    public void PoolExhaustion_ReturnsNullFromOffer_WithoutReusingAnActiveAddress()
    {
        var (manager, _) = Build("192.168.1.100", "192.168.1.101"); // two addresses
        manager.Commit(ClientA, manager.Offer(ClientA, null)!.Address);
        manager.Commit(ClientB, manager.Offer(ClientB, null)!.Address);

        var third = manager.Offer(ClientC, null);

        Assert.Null(third);
        Assert.Equal(2, manager.ActiveLeases.Count);
    }

    [Fact]
    public void Reservation_IsAlwaysOfferedToItsClient()
    {
        var time = TestTimeProvider.StartingAtEpoch();
        var reserved = IPv4Address.Parse("192.168.1.150");
        var pool = new DhcpAddressPool(
            IPv4Address.Parse("192.168.1.100"), IPv4Address.Parse("192.168.1.200"), Subnet, Server,
            reservations: new Dictionary<DhcpClientId, IPv4Address> { [ClientA] = reserved });
        var manager = new DhcpLeaseManager(pool, Server, TimeSpan.FromSeconds(3600), time);

        var lease = manager.Offer(ClientA, requestedAddress: IPv4Address.Parse("192.168.1.100"));

        Assert.Equal(reserved, lease!.Address);
    }

    [Fact]
    public void RequestedAddress_IsHonouredWhenFree()
    {
        var (manager, _) = Build();

        var lease = manager.Offer(ClientA, IPv4Address.Parse("192.168.1.107"));

        Assert.Equal(IPv4Address.Parse("192.168.1.107"), lease!.Address);
    }

    [Fact]
    public void CancelOffer_FreesAPendingOffer_ButLeavesAnActiveLeaseAlone()
    {
        var (manager, _) = Build();
        var offered = manager.Offer(ClientA, null)!;

        Assert.True(manager.CancelOffer(ClientA));
        Assert.False(manager.IsAddressInUse(offered.Address));

        var committed = manager.Commit(ClientB, manager.Offer(ClientB, null)!.Address).Lease!;
        Assert.False(manager.CancelOffer(ClientB));
        Assert.Contains(manager.ActiveLeases, l => l.Address == committed.Address);
    }
}
