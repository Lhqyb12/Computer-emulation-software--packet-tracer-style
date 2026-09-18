using NetSim.Core.Common.Exceptions;
using NetSim.Core.Networking;
using NetSim.Core.Tests.Arp;

namespace NetSim.Core.Tests.Networking;

public class ArpCacheTests
{
    private static readonly IPv4Address Ip1 = IPv4Address.Parse("192.168.1.1");
    private static readonly IPv4Address Ip20 = IPv4Address.Parse("192.168.1.20");
    private static readonly MacAddress MacA = MacAddress.Parse("00:11:22:33:44:55");
    private static readonly MacAddress MacB = MacAddress.Parse("AA:BB:CC:DD:EE:FF");

    [Fact]
    public void AddOrUpdateDynamic_ThenLookup_ReturnsTheMapping()
    {
        var cache = new ArpCache();

        var result = cache.AddOrUpdateDynamic(Ip20, MacB);

        Assert.Equal(ArpCacheChange.Added, result.Change);
        Assert.True(cache.Contains(Ip20));
        Assert.True(cache.TryResolve(Ip20, out var mac));
        Assert.Equal(MacB, mac);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void Lookup_UnknownAddress_Misses()
    {
        var cache = new ArpCache();

        Assert.False(cache.TryResolve(Ip20, out _));
        Assert.False(cache.Contains(Ip20));
        Assert.False(cache.TryGet(Ip20, out var entry));
        Assert.Null(entry);
    }

    [Fact]
    public void AddOrUpdateDynamic_SameMappingAgain_IsUpdate_NotConflict()
    {
        var cache = new ArpCache();
        cache.AddOrUpdateDynamic(Ip20, MacB);

        var result = cache.AddOrUpdateDynamic(Ip20, MacB);

        Assert.Equal(ArpCacheChange.Updated, result.Change);
        Assert.False(result.IsConflict);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void AddOrUpdateDynamic_DifferentMac_UpdatesDeterministically_AndFlagsConflict()
    {
        var cache = new ArpCache();
        cache.AddOrUpdateDynamic(Ip20, MacA);

        var result = cache.AddOrUpdateDynamic(Ip20, MacB);

        Assert.Equal(ArpCacheChange.Updated, result.Change);
        Assert.True(result.IsConflict);
        Assert.Equal(MacA, result.PreviousHardwareAddress);
        Assert.True(cache.TryResolve(Ip20, out var mac));
        Assert.Equal(MacB, mac); // last writer wins
        Assert.Equal(1, cache.Count); // no duplicate entry
    }

    [Fact]
    public void Remove_DropsTheEntry()
    {
        var cache = new ArpCache();
        cache.AddOrUpdateDynamic(Ip20, MacB);

        Assert.True(cache.Remove(Ip20));
        Assert.False(cache.Remove(Ip20));
        Assert.False(cache.Contains(Ip20));
    }

    [Fact]
    public void Clear_EmptiesTheCache()
    {
        var cache = new ArpCache();
        cache.AddOrUpdateDynamic(Ip1, MacA);
        cache.AddOrUpdateDynamic(Ip20, MacB);

        cache.Clear();

        Assert.Equal(0, cache.Count);
        Assert.Empty(cache.Entries);
    }

    [Fact]
    public void MultipleEntries_AreIndependentlyStored()
    {
        var cache = new ArpCache();
        cache.AddOrUpdateDynamic(Ip1, MacA);
        cache.AddOrUpdateDynamic(Ip20, MacB);

        Assert.Equal(2, cache.Count);
        Assert.True(cache.TryResolve(Ip1, out var a));
        Assert.True(cache.TryResolve(Ip20, out var b));
        Assert.Equal(MacA, a);
        Assert.Equal(MacB, b);
    }

    [Fact]
    public void DynamicEntry_ExpiresAfterItsLifetime_OnLookupAndCount()
    {
        var clock = TestTimeProvider.StartingAtEpoch();
        var cache = new ArpCache(clock, defaultDynamicLifetime: TimeSpan.FromMinutes(5));
        cache.AddOrUpdateDynamic(Ip20, MacB);

        clock.Advance(TimeSpan.FromMinutes(4));
        Assert.True(cache.TryResolve(Ip20, out _));

        clock.Advance(TimeSpan.FromMinutes(2)); // now 6 minutes in, past the 5-minute lifetime
        Assert.False(cache.TryResolve(Ip20, out _));
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void PruneExpired_RemovesAndReturnsExpiredEntries_LeavingLiveOnes()
    {
        var clock = TestTimeProvider.StartingAtEpoch();
        var cache = new ArpCache(clock, defaultDynamicLifetime: TimeSpan.FromMinutes(5));
        cache.AddOrUpdateDynamic(Ip1, MacA);

        clock.Advance(TimeSpan.FromMinutes(3));
        cache.AddOrUpdateDynamic(Ip20, MacB); // expires at T+8

        clock.Advance(TimeSpan.FromMinutes(3)); // T+6: Ip1 expired, Ip20 still live
        var removed = cache.PruneExpired();

        Assert.Single(removed);
        Assert.Equal(Ip1, removed[0].ProtocolAddress);
        Assert.True(cache.Contains(Ip20));
    }

    [Fact]
    public void StaticEntry_DoesNotExpire()
    {
        var clock = TestTimeProvider.StartingAtEpoch();
        var cache = new ArpCache(clock, defaultDynamicLifetime: TimeSpan.FromMinutes(1));
        cache.AddOrUpdateStatic(Ip20, MacB);

        clock.Advance(TimeSpan.FromDays(3650));

        Assert.True(cache.TryGet(Ip20, out var entry));
        Assert.Equal(ArpCacheEntryState.Static, entry!.State);
    }

    [Fact]
    public void DynamicLearn_DoesNotOverwriteAStaticEntry()
    {
        var cache = new ArpCache();
        cache.AddOrUpdateStatic(Ip20, MacA);

        var result = cache.AddOrUpdateDynamic(Ip20, MacB);

        Assert.Equal(ArpCacheChange.None, result.Change);
        Assert.True(cache.TryResolve(Ip20, out var mac));
        Assert.Equal(MacA, mac);
    }

    [Fact]
    public void StaticWrite_OverridesAnExistingDynamicEntry()
    {
        var cache = new ArpCache();
        cache.AddOrUpdateDynamic(Ip20, MacA);

        var result = cache.AddOrUpdateStatic(Ip20, MacB);

        Assert.Equal(ArpCacheChange.Updated, result.Change);
        Assert.True(cache.TryGet(Ip20, out var entry));
        Assert.Equal(ArpCacheEntryState.Static, entry!.State);
        Assert.Equal(MacB, entry.HardwareAddress);
    }

    [Fact]
    public void Constructor_RejectsNonPositiveDefaultLifetime()
    {
        Assert.Throws<DomainException>(() => new ArpCache(defaultDynamicLifetime: TimeSpan.Zero));
    }
}
