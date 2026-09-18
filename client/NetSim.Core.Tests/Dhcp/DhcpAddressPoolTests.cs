using System.Linq;
using NetSim.Core.Dhcp;
using NetSim.Core.Networking;

namespace NetSim.Core.Tests.Dhcp;

public class DhcpAddressPoolTests
{
    private static readonly IPv4Network Subnet = IPv4Network.Create(IPv4Address.Parse("192.168.1.0"), 24);
    private static readonly IPv4Address Server = IPv4Address.Parse("192.168.1.1");
    private static readonly DhcpClientId ClientA = DhcpClientId.FromHardwareAddress(MacAddress.Parse("AA:BB:CC:00:00:0A"));
    private static readonly DhcpClientId ClientB = DhcpClientId.FromHardwareAddress(MacAddress.Parse("AA:BB:CC:00:00:0B"));

    private static DhcpAddressPool Pool(
        string start = "192.168.1.100", string end = "192.168.1.110",
        IEnumerable<IPv4Address>? excluded = null, IReadOnlyDictionary<DhcpClientId, IPv4Address>? reservations = null) =>
        new(IPv4Address.Parse(start), IPv4Address.Parse(end), Subnet, Server, excluded, reservations);

    [Fact]
    public void IsInRange_IsTrueOnlyBetweenStartAndEndInclusive()
    {
        var pool = Pool();

        Assert.True(pool.IsInRange(IPv4Address.Parse("192.168.1.100")));
        Assert.True(pool.IsInRange(IPv4Address.Parse("192.168.1.110")));
        Assert.False(pool.IsInRange(IPv4Address.Parse("192.168.1.99")));
        Assert.False(pool.IsInRange(IPv4Address.Parse("192.168.1.111")));
    }

    [Fact]
    public void AssignableAddresses_SkipsNetworkBroadcastServerAndExcludedAddresses()
    {
        var pool = Pool("192.168.1.0", "192.168.1.255", excluded: [IPv4Address.Parse("192.168.1.100")]);

        var assignable = pool.AssignableAddresses(ClientA).Take(5).ToList();

        Assert.DoesNotContain(IPv4Address.Parse("192.168.1.0"), assignable);   // network
        Assert.DoesNotContain(IPv4Address.Parse("192.168.1.255"), assignable); // broadcast
        Assert.DoesNotContain(Server, assignable);                             // server
        Assert.DoesNotContain(IPv4Address.Parse("192.168.1.100"), assignable); // excluded
        Assert.Equal(IPv4Address.Parse("192.168.1.2"), assignable[0]);
    }

    [Fact]
    public void Reservation_IsReturnedForItsClient_AndBlockedForEveryoneElse()
    {
        var reserved = IPv4Address.Parse("192.168.1.105");
        var pool = Pool(reservations: new Dictionary<DhcpClientId, IPv4Address> { [ClientA] = reserved });

        Assert.Equal(reserved, pool.ReservationFor(ClientA));
        Assert.True(pool.IsAssignableTo(reserved, ClientA));
        Assert.False(pool.IsAssignableTo(reserved, ClientB));
        Assert.DoesNotContain(reserved, pool.AssignableAddresses(ClientB).ToList());
    }

    [Fact]
    public void IsReservedInfrastructure_CoversNetworkBroadcastAndServer()
    {
        var pool = Pool();

        Assert.True(pool.IsReservedInfrastructure(IPv4Address.Parse("192.168.1.0")));
        Assert.True(pool.IsReservedInfrastructure(IPv4Address.Parse("192.168.1.255")));
        Assert.True(pool.IsReservedInfrastructure(Server));
        Assert.False(pool.IsReservedInfrastructure(IPv4Address.Parse("192.168.1.100")));
    }

    [Fact]
    public void AssignableAddresses_OfASmallPool_EnumeratesExactlyThatManyAddresses()
    {
        var pool = Pool("192.168.1.100", "192.168.1.102");

        Assert.Equal(
            new[] { "192.168.1.100", "192.168.1.101", "192.168.1.102" }.Select(IPv4Address.Parse),
            pool.AssignableAddresses(ClientA));
    }
}
