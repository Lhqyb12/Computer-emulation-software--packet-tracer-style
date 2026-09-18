using NetSim.Core.Devices;
using NetSim.Core.Networking;
using NetSim.Core.Switching;
using NetSim.Core.Vlans;

namespace NetSim.Core.Tests.Switching;

/// <summary>
/// Phase 26 - the MAC address table keyed by <c>(VLAN, MAC)</c>: the same address in two VLANs,
/// VLAN-scoped lookup, VLAN-scoped removal, and the MAC-only overloads still acting on VLAN 1.
/// </summary>
public class VlanAwareMacAddressTableTests
{
    private static readonly MacAddress A = MacAddress.Parse("02:00:00:00:00:AA");
    private static readonly MacAddress B = MacAddress.Parse("02:00:00:00:00:BB");
    private static readonly VlanId Vlan10 = new(10);
    private static readonly VlanId Vlan20 = new(20);

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
    public void TheSameMac_CanBeLearnedInTwoVlans_OnDifferentPorts_Independently()
    {
        var p = new Ports();
        var table = new MacAddressTable();

        table.Learn(Vlan10, A, p.P1, TimeSpan.Zero);
        table.Learn(Vlan20, A, p.P3, TimeSpan.Zero);

        Assert.Equal(2, table.Count);
        Assert.Same(p.P1, table.Lookup(Vlan10, A)!.Port);
        Assert.Same(p.P3, table.Lookup(Vlan20, A)!.Port);
        Assert.Equal(Vlan10, table.Lookup(Vlan10, A)!.Vlan);
    }

    [Fact]
    public void LearningInOneVlan_DoesNotOverwriteTheOtherVlansEntry()
    {
        var p = new Ports();
        var table = new MacAddressTable();
        table.Learn(Vlan10, A, p.P1, TimeSpan.Zero);
        table.Learn(Vlan20, A, p.P2, TimeSpan.Zero);

        // Move A within VLAN 20 only.
        var moved = table.Learn(Vlan20, A, p.P3, TimeSpan.FromSeconds(1));

        Assert.Equal(MacLearnOutcome.Moved, moved.Outcome);
        Assert.Same(p.P1, table.Lookup(Vlan10, A)!.Port); // VLAN 10 untouched
        Assert.Same(p.P3, table.Lookup(Vlan20, A)!.Port);
    }

    [Fact]
    public void Lookup_IsScopedToTheVlan()
    {
        var p = new Ports();
        var table = new MacAddressTable();
        table.Learn(Vlan10, A, p.P1, TimeSpan.Zero);

        Assert.NotNull(table.Lookup(Vlan10, A));
        Assert.Null(table.Lookup(Vlan20, A));
        Assert.False(table.Contains(Vlan20, A));
        Assert.True(table.Contains(Vlan10, A));
    }

    [Fact]
    public void Remove_IsScopedToTheVlan()
    {
        var p = new Ports();
        var table = new MacAddressTable();
        table.Learn(Vlan10, A, p.P1, TimeSpan.Zero);
        table.Learn(Vlan20, A, p.P2, TimeSpan.Zero);

        Assert.True(table.Remove(Vlan10, A));
        Assert.False(table.Contains(Vlan10, A));
        Assert.True(table.Contains(Vlan20, A));
    }

    [Fact]
    public void RemoveEntriesForPort_RemovesEveryVlansEntriesOnThatPort()
    {
        var p = new Ports();
        var table = new MacAddressTable();
        table.Learn(Vlan10, A, p.P1, TimeSpan.Zero);
        table.Learn(Vlan20, B, p.P1, TimeSpan.Zero);
        table.Learn(Vlan10, B, p.P2, TimeSpan.Zero);

        var removed = table.RemoveEntriesForPort(p.P1.Id);

        Assert.Equal(2, removed.Count);
        Assert.Empty(table.GetEntriesForPort(p.P1.Id));
        Assert.True(table.Contains(Vlan10, B)); // on P2, untouched
    }

    [Fact]
    public void RemoveEntriesForVlanOnPort_RemovesOnlyThatVlansEntriesOnThatPort()
    {
        var p = new Ports();
        var table = new MacAddressTable();
        table.Learn(Vlan10, A, p.P1, TimeSpan.Zero);
        table.Learn(Vlan20, B, p.P1, TimeSpan.Zero);

        var removed = table.RemoveEntriesForVlanOnPort(Vlan10, p.P1.Id);

        Assert.Single(removed);
        Assert.False(table.Contains(Vlan10, A));
        Assert.True(table.Contains(Vlan20, B));
    }

    [Fact]
    public void RemoveEntriesForVlan_RemovesEveryPortsEntriesInThatVlan()
    {
        var p = new Ports();
        var table = new MacAddressTable();
        table.Learn(Vlan10, A, p.P1, TimeSpan.Zero);
        table.Learn(Vlan10, B, p.P2, TimeSpan.Zero);
        table.Learn(Vlan20, A, p.P3, TimeSpan.Zero);

        var removed = table.RemoveEntriesForVlan(Vlan10);

        Assert.Equal(2, removed.Count);
        Assert.Equal(1, table.Count);
        Assert.True(table.Contains(Vlan20, A));
    }

    [Fact]
    public void Aging_IsIndependentPerVlanEntry()
    {
        var p = new Ports();
        var table = new MacAddressTable { AgingTime = TimeSpan.FromSeconds(300) };
        table.Learn(Vlan10, A, p.P1, TimeSpan.FromSeconds(0));
        table.Learn(Vlan20, A, p.P2, TimeSpan.FromSeconds(200));

        var expired = table.AgeEntries(TimeSpan.FromSeconds(310));

        Assert.Single(expired);
        Assert.Equal(Vlan10, expired[0].Vlan);
        Assert.True(table.Contains(Vlan20, A));
    }

    [Fact]
    public void TheMacOnlyOverloads_ActOnTheDefaultVlan()
    {
        var p = new Ports();
        var table = new MacAddressTable();

        table.Learn(A, p.P1, TimeSpan.Zero); // MAC-only

        Assert.Same(p.P1, table.Lookup(VlanId.Default, A)!.Port);
        Assert.Same(p.P1, table.Lookup(A)!.Port);
        Assert.Equal(VlanId.Default, table.Lookup(A)!.Vlan);
    }
}
