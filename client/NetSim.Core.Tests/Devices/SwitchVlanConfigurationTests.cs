using NetSim.Core.Common.Exceptions;
using NetSim.Core.Devices;
using NetSim.Core.Networking;
using NetSim.Core.Vlans;

namespace NetSim.Core.Tests.Devices;

/// <summary>
/// Phase 26 - the switch's own VLAN configuration surface: the seeded default VLAN, safe VLAN
/// removal, port-mode changes, configuration validation, MAC-table invalidation on a VLAN change
/// and the configuration-changed events.
/// </summary>
public class SwitchVlanConfigurationTests
{
    private static readonly VlanId Vlan10 = new(10);
    private static readonly VlanId Vlan20 = new(20);

    private static (Switch Switch, NetworkInterface P1, NetworkInterface P2) NewSwitch()
    {
        var @switch = (Switch)NetworkDeviceFactory.Create(DeviceType.Switch, "SW1");
        return (@switch,
            @switch.Interfaces.First(i => i.Name == "FastEthernet0/1"),
            @switch.Interfaces.First(i => i.Name == "FastEthernet0/2"));
    }

    [Fact]
    public void ANewSwitch_HasTheDefaultVlan_AndEveryPortIsAnAccessPortInIt()
    {
        var (@switch, p1, _) = NewSwitch();

        Assert.True(@switch.Vlans.Exists(VlanId.Default));
        var config = @switch.GetPortVlanConfiguration(p1);
        Assert.Equal(SwitchPortMode.Access, config.Mode);
        Assert.Equal(VlanId.Default, config.AccessVlan);
    }

    [Fact]
    public void ConfigureAccessPort_WithAnUndefinedVlan_Throws()
    {
        var (@switch, p1, _) = NewSwitch();

        Assert.Throws<DomainException>(() => @switch.ConfigureAccessPort(p1, Vlan10));
    }

    [Fact]
    public void ConfigureAccessPort_RaisesPortConfigurationChanged()
    {
        var (@switch, p1, _) = NewSwitch();
        @switch.CreateVlan(Vlan10);
        SwitchPortConfigurationChangedEventArgs? change = null;
        @switch.PortConfigurationChanged += (_, e) => change = e;

        @switch.ConfigureAccessPort(p1, Vlan10);

        Assert.NotNull(change);
        Assert.Same(p1, change!.Port);
        Assert.Equal(Vlan10, @switch.GetPortVlanConfiguration(p1).AccessVlan);
    }

    [Fact]
    public void ChangingAccessVlan_FlushesEveryMacEntryLearnedOnThatPort()
    {
        var (@switch, p1, _) = NewSwitch();
        @switch.CreateVlan(Vlan10);
        @switch.CreateVlan(Vlan20);
        @switch.ConfigureAccessPort(p1, Vlan10);
        @switch.MacAddressTable.Learn(Vlan10, MacAddress.Parse("02:00:00:00:00:AA"), p1, TimeSpan.Zero);

        @switch.SetAccessVlan(p1, Vlan20);

        Assert.Empty(@switch.MacAddressTable.GetEntriesForPort(p1.Id));
    }

    [Fact]
    public void SetAccessVlan_OnATrunkPort_Throws()
    {
        var (@switch, p1, _) = NewSwitch();
        @switch.CreateVlan(Vlan10);
        @switch.ConfigureTrunkPort(p1);

        Assert.Throws<DomainException>(() => @switch.SetAccessVlan(p1, Vlan10));
    }

    [Fact]
    public void SetPortMode_TrunkThenAccess_ResetsIncompatibleFields_AndFlushesMacEntries()
    {
        var (@switch, p1, _) = NewSwitch();
        @switch.CreateVlan(Vlan10);
        @switch.ConfigureTrunkPort(p1, nativeVlan: VlanId.Default, allowedVlans: [Vlan10]);
        @switch.MacAddressTable.Learn(Vlan10, MacAddress.Parse("02:00:00:00:00:AA"), p1, TimeSpan.Zero);

        @switch.SetPortMode(p1, SwitchPortMode.Access);

        var config = @switch.GetPortVlanConfiguration(p1);
        Assert.True(config.IsAccess);
        Assert.Equal(VlanId.Default, config.AccessVlan);
        Assert.Empty(config.AllowedVlans);
        Assert.Empty(@switch.MacAddressTable.GetEntriesForPort(p1.Id));
    }

    [Fact]
    public void RemoveVlan_NeverRemovesTheDefaultVlan()
    {
        var (@switch, _, _) = NewSwitch();

        Assert.Throws<DomainException>(() => @switch.RemoveVlan(VlanId.Default));
    }

    [Fact]
    public void RemoveVlan_WhileAPortStillReferencesIt_Throws()
    {
        var (@switch, p1, _) = NewSwitch();
        @switch.CreateVlan(Vlan10);
        @switch.ConfigureAccessPort(p1, Vlan10);

        Assert.Throws<DomainException>(() => @switch.RemoveVlan(Vlan10));
        Assert.True(@switch.IsVlanInUse(Vlan10));
    }

    [Fact]
    public void RemoveVlan_AfterReassigningTheReferencingPort_Succeeds()
    {
        var (@switch, p1, _) = NewSwitch();
        @switch.CreateVlan(Vlan10);
        @switch.ConfigureAccessPort(p1, Vlan10);
        @switch.ConfigureAccessPort(p1, VlanId.Default);

        Assert.True(@switch.RemoveVlan(Vlan10));
        Assert.False(@switch.Vlans.Exists(Vlan10));
    }

    [Fact]
    public void RemoveVlan_WhileATrunkAllowsIt_Throws()
    {
        var (@switch, p1, _) = NewSwitch();
        @switch.CreateVlan(Vlan10);
        @switch.ConfigureTrunkPort(p1, nativeVlan: VlanId.Default, allowedVlans: [Vlan10]);

        Assert.Throws<DomainException>(() => @switch.RemoveVlan(Vlan10));
    }

    [Fact]
    public void ConfigureTrunkPort_WithAnUndefinedAllowedVlan_Throws()
    {
        var (@switch, p1, _) = NewSwitch();

        Assert.Throws<DomainException>(() =>
            @switch.ConfigureTrunkPort(p1, nativeVlan: VlanId.Default, allowedVlans: [Vlan10]));
    }

    [Fact]
    public void GetPortVlanConfiguration_ForAPortOnAnotherDevice_Throws()
    {
        var (@switch, _, _) = NewSwitch();
        var pc = NetworkDeviceFactory.Create(DeviceType.Pc, "PC1");

        Assert.Throws<DomainException>(() => @switch.GetPortVlanConfiguration(pc.Interfaces.Single()));
    }
}
