using NetSim.Core.Common.Exceptions;
using NetSim.Core.Networking;

namespace NetSim.Core.Tests.Networking;

public class ArpCacheEntryTests
{
    private static readonly IPv4Address Ip = IPv4Address.Parse("192.168.1.20");
    private static readonly MacAddress Mac = MacAddress.Parse("AA:BB:CC:DD:EE:FF");
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Dynamic_CarriesTheMapping_AndComputesExpiry()
    {
        var entry = ArpCacheEntry.Dynamic(Ip, Mac, T0, TimeSpan.FromMinutes(10));

        Assert.Equal(Ip, entry.ProtocolAddress);
        Assert.Equal(Mac, entry.HardwareAddress);
        Assert.Equal(ArpCacheEntryState.Dynamic, entry.State);
        Assert.True(entry.IsDynamic);
        Assert.False(entry.IsStatic);
        Assert.Equal(T0, entry.CreatedAtUtc);
        Assert.Equal(T0.AddMinutes(10), entry.ExpiresAtUtc);
    }

    [Fact]
    public void Dynamic_IsExpired_OnlyAtOrAfterExpiry()
    {
        var entry = ArpCacheEntry.Dynamic(Ip, Mac, T0, TimeSpan.FromMinutes(10));

        Assert.False(entry.IsExpired(T0));
        Assert.False(entry.IsExpired(T0.AddMinutes(9).AddSeconds(59)));
        Assert.True(entry.IsExpired(T0.AddMinutes(10)));
        Assert.True(entry.IsExpired(T0.AddHours(1)));
    }

    [Fact]
    public void Dynamic_TimeToLive_CountsDownThenClampsToZero()
    {
        var entry = ArpCacheEntry.Dynamic(Ip, Mac, T0, TimeSpan.FromMinutes(10));

        Assert.Equal(TimeSpan.FromMinutes(10), entry.TimeToLive(T0));
        Assert.Equal(TimeSpan.FromMinutes(4), entry.TimeToLive(T0.AddMinutes(6)));
        Assert.Equal(TimeSpan.Zero, entry.TimeToLive(T0.AddMinutes(30)));
    }

    [Fact]
    public void Static_NeverExpires_AndHasNoExpiry()
    {
        var entry = ArpCacheEntry.Static(Ip, Mac, T0);

        Assert.Equal(ArpCacheEntryState.Static, entry.State);
        Assert.True(entry.IsStatic);
        Assert.Null(entry.ExpiresAtUtc);
        Assert.False(entry.IsExpired(T0.AddYears(10)));
        Assert.Null(entry.TimeToLive(T0));
    }

    [Theory]
    [InlineData("0.0.0.0")]
    [InlineData("255.255.255.255")]
    [InlineData("224.0.0.1")]
    public void Factory_RejectsNonHostIPv4(string address)
    {
        var ip = IPv4Address.Parse(address);

        Assert.Throws<DomainException>(() => ArpCacheEntry.Dynamic(ip, Mac, T0, TimeSpan.FromMinutes(1)));
        Assert.Throws<DomainException>(() => ArpCacheEntry.Static(ip, Mac, T0));
    }

    [Fact]
    public void Factory_RejectsBroadcastAndZeroMac()
    {
        Assert.Throws<DomainException>(() => ArpCacheEntry.Dynamic(Ip, MacAddress.Broadcast, T0, TimeSpan.FromMinutes(1)));
        Assert.Throws<DomainException>(() => ArpCacheEntry.Static(Ip, MacAddress.Zero, T0));
    }

    [Fact]
    public void Dynamic_RejectsNonPositiveLifetime()
    {
        Assert.Throws<DomainException>(() => ArpCacheEntry.Dynamic(Ip, Mac, T0, TimeSpan.Zero));
        Assert.Throws<DomainException>(() => ArpCacheEntry.Dynamic(Ip, Mac, T0, TimeSpan.FromSeconds(-1)));
    }
}
