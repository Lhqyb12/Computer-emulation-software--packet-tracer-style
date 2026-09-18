using NetSim.Core.Devices;
using NetSim.Core.Dns;
using NetSim.Core.Networking;
using NetSim.Core.Tests.Arp;

namespace NetSim.Core.Tests.Dns;

public class DnsCacheTests
{
    private static readonly DomainName Www = DomainName.Parse("www.example.com");

    private static NetworkDevice CreateDevice() => NetworkDeviceFactory.Create(DeviceType.Pc, "PC0");

    [Fact]
    public void TryGet_BeforeAnyPut_IsAMiss()
    {
        var cache = new DnsCache(TestTimeProvider.StartingAtEpoch());

        Assert.False(cache.TryGet(CreateDevice(), Www, DnsRecordType.A, out _));
    }

    [Fact]
    public void Put_ThenTryGet_IsAHit()
    {
        var time = TestTimeProvider.StartingAtEpoch();
        var cache = new DnsCache(time);
        var device = CreateDevice();
        var records = new[] { new DnsARecord(Www, IPv4Address.Parse("192.168.1.100"), TimeSpan.FromSeconds(300)) };

        cache.Put(device, Www, DnsRecordType.A, records);

        Assert.True(cache.TryGet(device, Www, DnsRecordType.A, out var found));
        Assert.Equal(records, found);
    }

    [Fact]
    public void Entry_ExpiresAfterItsTtl()
    {
        var time = TestTimeProvider.StartingAtEpoch();
        var cache = new DnsCache(time);
        var device = CreateDevice();
        var records = new[] { new DnsARecord(Www, IPv4Address.Parse("192.168.1.100"), TimeSpan.FromSeconds(300)) };
        cache.Put(device, Www, DnsRecordType.A, records);

        time.Advance(TimeSpan.FromSeconds(299));
        Assert.True(cache.TryGet(device, Www, DnsRecordType.A, out _));

        time.Advance(TimeSpan.FromSeconds(2));
        Assert.False(cache.TryGet(device, Www, DnsRecordType.A, out _));
    }

    [Fact]
    public void Put_UsesTheSmallestTtlAmongTheRecords()
    {
        var time = TestTimeProvider.StartingAtEpoch();
        var cache = new DnsCache(time);
        var device = CreateDevice();
        var records = new DnsRecord[]
        {
            new DnsARecord(Www, IPv4Address.Parse("192.168.1.100"), TimeSpan.FromSeconds(300)),
            new DnsARecord(Www, IPv4Address.Parse("192.168.1.101"), TimeSpan.FromSeconds(10)),
        };
        cache.Put(device, Www, DnsRecordType.A, records);

        time.Advance(TimeSpan.FromSeconds(11));

        Assert.False(cache.TryGet(device, Www, DnsRecordType.A, out _));
    }

    [Fact]
    public void Put_ZeroTtl_IsNotCached()
    {
        var cache = new DnsCache(TestTimeProvider.StartingAtEpoch());
        var device = CreateDevice();
        var records = new[] { new DnsARecord(Www, IPv4Address.Parse("192.168.1.100"), TimeSpan.Zero) };

        cache.Put(device, Www, DnsRecordType.A, records);

        Assert.False(cache.TryGet(device, Www, DnsRecordType.A, out _));
    }

    [Fact]
    public void Put_EmptyRecordSet_IsNotCached()
    {
        var cache = new DnsCache(TestTimeProvider.StartingAtEpoch());
        var device = CreateDevice();

        cache.Put(device, Www, DnsRecordType.A, []);

        Assert.False(cache.TryGet(device, Www, DnsRecordType.A, out _));
    }

    [Fact]
    public void Remove_DropsTheEntry()
    {
        var cache = new DnsCache(TestTimeProvider.StartingAtEpoch());
        var device = CreateDevice();
        cache.Put(device, Www, DnsRecordType.A, [new DnsARecord(Www, IPv4Address.Parse("192.168.1.100"), TimeSpan.FromSeconds(300))]);

        Assert.True(cache.Remove(device, Www, DnsRecordType.A));
        Assert.False(cache.TryGet(device, Www, DnsRecordType.A, out _));
    }

    [Fact]
    public void DifferentDevices_HaveIndependentCaches()
    {
        var cache = new DnsCache(TestTimeProvider.StartingAtEpoch());
        var device1 = CreateDevice();
        var device2 = CreateDevice();
        cache.Put(device1, Www, DnsRecordType.A, [new DnsARecord(Www, IPv4Address.Parse("192.168.1.100"), TimeSpan.FromSeconds(300))]);

        Assert.True(cache.TryGet(device1, Www, DnsRecordType.A, out _));
        Assert.False(cache.TryGet(device2, Www, DnsRecordType.A, out _));
    }

    [Fact]
    public void Clear_RemovesOnlyThatDevicesEntries()
    {
        var cache = new DnsCache(TestTimeProvider.StartingAtEpoch());
        var device1 = CreateDevice();
        var device2 = CreateDevice();
        cache.Put(device1, Www, DnsRecordType.A, [new DnsARecord(Www, IPv4Address.Parse("192.168.1.100"), TimeSpan.FromSeconds(300))]);
        cache.Put(device2, Www, DnsRecordType.A, [new DnsARecord(Www, IPv4Address.Parse("192.168.1.100"), TimeSpan.FromSeconds(300))]);

        cache.Clear(device1);

        Assert.False(cache.TryGet(device1, Www, DnsRecordType.A, out _));
        Assert.True(cache.TryGet(device2, Www, DnsRecordType.A, out _));
    }

    [Fact]
    public void PruneExpired_ReturnsAndRemovesOnlyExpiredEntries()
    {
        var time = TestTimeProvider.StartingAtEpoch();
        var cache = new DnsCache(time);
        var device = CreateDevice();
        cache.Put(device, Www, DnsRecordType.A, [new DnsARecord(Www, IPv4Address.Parse("192.168.1.100"), TimeSpan.FromSeconds(5))]);
        var other = DomainName.Parse("example.com");
        cache.Put(device, other, DnsRecordType.A, [new DnsARecord(other, IPv4Address.Parse("192.168.1.200"), TimeSpan.FromSeconds(300))]);

        time.Advance(TimeSpan.FromSeconds(10));
        var expired = cache.PruneExpired(device);

        Assert.Single(expired);
        Assert.Equal(Www, expired[0].Name);
        Assert.True(cache.TryGet(device, other, DnsRecordType.A, out _));
    }
}
