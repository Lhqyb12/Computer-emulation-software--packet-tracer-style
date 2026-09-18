using NetSim.Core.Common.Exceptions;
using NetSim.Core.Devices;
using NetSim.Core.Networking;
using NetSim.Core.Switching;

namespace NetSim.Core.Tests.Switching;

/// <summary>
/// Phase 25 - the per-switch MAC address forwarding table in isolation: dynamic learning,
/// refresh, move, lookup, removal, port-change pruning and aging on explicit simulation time.
/// </summary>
public class MacAddressTableTests
{
    // First octet 0x02 => unicast (I/G bit clear), locally administered (U/L bit set).
    private static readonly MacAddress A = MacAddress.Parse("02:00:00:00:00:AA");
    private static readonly MacAddress B = MacAddress.Parse("02:00:00:00:00:BB");

    private sealed class Ports
    {
        public NetworkDevice Switch { get; } = NetworkDeviceFactory.Create(DeviceType.Switch, "SW1");
        public NetworkInterface P1 { get; }
        public NetworkInterface P2 { get; }
        public NetworkInterface P3 { get; }

        public Ports()
        {
            P1 = Switch.Interfaces.First(i => i.Name == "FastEthernet0/1");
            P2 = Switch.Interfaces.First(i => i.Name == "FastEthernet0/2");
            P3 = Switch.Interfaces.First(i => i.Name == "FastEthernet0/3");
        }
    }

    [Fact]
    public void Learn_NewAddress_AddsADynamicEntry()
    {
        var p = new Ports();
        var table = new MacAddressTable();

        var result = table.Learn(A, p.P1, TimeSpan.FromSeconds(10));

        Assert.Equal(MacLearnOutcome.Added, result.Outcome);
        Assert.Equal(1, table.Count);
        var entry = table.Lookup(A);
        Assert.NotNull(entry);
        Assert.Same(p.P1, entry!.Port);
        Assert.Equal(MacTableEntryType.Dynamic, entry.Type);
        Assert.Equal(TimeSpan.FromSeconds(10), entry.LearnedAt);
        Assert.Equal(TimeSpan.FromSeconds(10), entry.LastSeenAt);
    }

    [Fact]
    public void Learn_SameAddressSamePort_RefreshesLastSeen_ButKeepsLearnedAt()
    {
        var p = new Ports();
        var table = new MacAddressTable();
        table.Learn(A, p.P1, TimeSpan.FromSeconds(10));

        var result = table.Learn(A, p.P1, TimeSpan.FromSeconds(90));

        Assert.Equal(MacLearnOutcome.Refreshed, result.Outcome);
        var entry = table.Lookup(A)!;
        Assert.Equal(TimeSpan.FromSeconds(10), entry.LearnedAt);
        Assert.Equal(TimeSpan.FromSeconds(90), entry.LastSeenAt);
    }

    [Fact]
    public void Learn_SameAddressDifferentPort_MovesTheEntry_AndReportsThePreviousPort()
    {
        var p = new Ports();
        var table = new MacAddressTable();
        table.Learn(A, p.P1, TimeSpan.FromSeconds(10));

        var result = table.Learn(A, p.P3, TimeSpan.FromSeconds(20));

        Assert.Equal(MacLearnOutcome.Moved, result.Outcome);
        Assert.Same(p.P1, result.PreviousPort);
        Assert.Same(p.P3, table.Lookup(A)!.Port);
        Assert.Equal(1, table.Count);
    }

    [Fact]
    public void Learn_NonUnicastSource_IsIgnored()
    {
        var p = new Ports();
        var table = new MacAddressTable();

        Assert.Equal(MacLearnOutcome.Unchanged, table.Learn(MacAddress.Broadcast, p.P1, TimeSpan.Zero).Outcome);
        Assert.Equal(MacLearnOutcome.Unchanged, table.Learn(MacAddress.Zero, p.P1, TimeSpan.Zero).Outcome);
        Assert.Equal(0, table.Count);
    }

    [Fact]
    public void TryGetPort_And_Contains_ReflectLearningState()
    {
        var p = new Ports();
        var table = new MacAddressTable();
        table.Learn(A, p.P2, TimeSpan.Zero);

        Assert.True(table.Contains(A));
        Assert.True(table.TryGetPort(A, out var port));
        Assert.Same(p.P2, port);
        Assert.False(table.Contains(B));
        Assert.False(table.TryGetPort(B, out _));
    }

    [Fact]
    public void Remove_And_Clear_DropEntries()
    {
        var p = new Ports();
        var table = new MacAddressTable();
        table.Learn(A, p.P1, TimeSpan.Zero);
        table.Learn(B, p.P2, TimeSpan.Zero);

        Assert.True(table.Remove(A));
        Assert.False(table.Remove(A));
        Assert.Equal(1, table.Count);

        table.Clear();
        Assert.Equal(0, table.Count);
    }

    [Fact]
    public void RemoveEntriesForPort_DropsOnlyThatPortsEntries()
    {
        var p = new Ports();
        var table = new MacAddressTable();
        table.Learn(A, p.P1, TimeSpan.Zero);
        table.Learn(B, p.P2, TimeSpan.Zero);

        var removed = table.RemoveEntriesForPort(p.P1.Id);

        Assert.Single(removed);
        Assert.Equal(A, removed[0].MacAddress);
        Assert.True(table.Contains(B));
        Assert.False(table.Contains(A));
    }

    [Fact]
    public void AgeEntries_RemovesEntriesUnusedForLongerThanAgingTime()
    {
        var p = new Ports();
        var table = new MacAddressTable { AgingTime = TimeSpan.FromSeconds(300) };
        table.Learn(A, p.P1, TimeSpan.FromSeconds(0));
        table.Learn(B, p.P2, TimeSpan.FromSeconds(200));

        // At t = 310s: A has been idle 310s (expired), B idle 110s (still live).
        var expired = table.AgeEntries(TimeSpan.FromSeconds(310));

        Assert.Single(expired);
        Assert.Equal(A, expired[0].MacAddress);
        Assert.False(table.Contains(A));
        Assert.True(table.Contains(B));
    }

    [Fact]
    public void AgeEntries_DoesNotRemoveARefreshedEntry()
    {
        var p = new Ports();
        var table = new MacAddressTable { AgingTime = TimeSpan.FromSeconds(300) };
        table.Learn(A, p.P1, TimeSpan.FromSeconds(0));
        table.Learn(A, p.P1, TimeSpan.FromSeconds(280)); // refresh keeps it alive

        Assert.Empty(table.AgeEntries(TimeSpan.FromSeconds(310)));
        Assert.True(table.Contains(A));
    }

    [Fact]
    public void AgingTime_MustBePositive()
    {
        var table = new MacAddressTable();
        Assert.Throws<DomainException>(() => table.AgingTime = TimeSpan.Zero);
        Assert.Throws<DomainException>(() => table.AgingTime = TimeSpan.FromSeconds(-1));
    }

    [Fact]
    public void DefaultAgingTime_IsTheClassicFiveMinutes()
    {
        Assert.Equal(TimeSpan.FromSeconds(300), new MacAddressTable().AgingTime);
        Assert.Equal(TimeSpan.FromSeconds(300), MacAddressTable.DefaultAgingTime);
    }

    [Fact]
    public void GetEntries_ReturnsNewestLearnedFirst()
    {
        var p = new Ports();
        var table = new MacAddressTable();
        table.Learn(A, p.P1, TimeSpan.FromSeconds(1));
        table.Learn(B, p.P2, TimeSpan.FromSeconds(5));

        var entries = table.GetEntries();

        Assert.Equal([B, A], entries.Select(e => e.MacAddress));
    }
}
