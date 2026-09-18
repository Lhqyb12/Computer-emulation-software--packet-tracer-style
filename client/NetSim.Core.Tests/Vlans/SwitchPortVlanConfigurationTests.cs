using NetSim.Core.Vlans;

namespace NetSim.Core.Tests.Vlans;

/// <summary>Phase 26 - the per-port access/trunk configuration holder in isolation.</summary>
public class SwitchPortVlanConfigurationTests
{
    [Fact]
    public void ANewPort_IsAnAccessPortInTheDefaultVlan()
    {
        var config = new SwitchPortVlanConfiguration();

        Assert.Equal(SwitchPortMode.Access, config.Mode);
        Assert.True(config.IsAccess);
        Assert.Equal(VlanId.Default, config.AccessVlan);
        Assert.True(config.CarriesVlan(VlanId.Default));
        Assert.False(config.CarriesVlan(new VlanId(10)));
    }

    [Fact]
    public void ConfigureAsAccess_SetsTheSingleVlan_AndCarriesOnlyThatVlan()
    {
        var config = new SwitchPortVlanConfiguration();
        config.ConfigureAsAccess(new VlanId(10));

        Assert.True(config.CarriesVlan(new VlanId(10)));
        Assert.False(config.CarriesVlan(new VlanId(20)));
        Assert.False(config.CarriesVlan(VlanId.Default));
    }

    [Fact]
    public void ConfigureAsTrunk_WithNoAllowList_CarriesEveryVlan()
    {
        var config = new SwitchPortVlanConfiguration();
        config.ConfigureAsTrunk();

        Assert.True(config.IsTrunk);
        Assert.True(config.AllowsAllVlans);
        Assert.True(config.CarriesVlan(new VlanId(10)));
        Assert.True(config.CarriesVlan(new VlanId(4094)));
    }

    [Fact]
    public void ConfigureAsTrunk_WithAnAllowList_CarriesOnlyListedVlans()
    {
        var config = new SwitchPortVlanConfiguration();
        config.ConfigureAsTrunk(nativeVlan: VlanId.Default, allowedVlans: [new VlanId(10), new VlanId(20)]);

        Assert.False(config.AllowsAllVlans);
        Assert.True(config.CarriesVlan(new VlanId(10)));
        Assert.True(config.CarriesVlan(new VlanId(20)));
        Assert.False(config.CarriesVlan(new VlanId(30)));
        Assert.Equal([10, 20], config.AllowedVlans.Select(v => v.Value));
    }

    [Fact]
    public void AllowVlan_And_RemoveAllowedVlan_EditTheList()
    {
        var config = new SwitchPortVlanConfiguration();
        config.ConfigureAsTrunk(allowedVlans: [new VlanId(10)]);

        config.AllowVlan(new VlanId(20));
        Assert.True(config.CarriesVlan(new VlanId(20)));

        config.RemoveAllowedVlan(new VlanId(10));
        Assert.False(config.CarriesVlan(new VlanId(10)));
        Assert.Equal([20], config.AllowedVlans.Select(v => v.Value));
    }

    [Fact]
    public void SwitchingFromTrunkToAccess_DiscardsTheTrunkConfiguration()
    {
        var config = new SwitchPortVlanConfiguration();
        config.ConfigureAsTrunk(nativeVlan: new VlanId(99), allowedVlans: [new VlanId(10)]);

        config.ConfigureAsAccess(new VlanId(10));

        Assert.True(config.IsAccess);
        Assert.Equal(VlanId.Default, config.NativeVlan);
        Assert.False(config.AllowsAllVlans is false && config.AllowedVlans.Count > 0);
        Assert.Empty(config.AllowedVlans);
    }

    [Fact]
    public void SwitchingFromAccessToTrunk_ResetsTheAccessVlanToDefault()
    {
        var config = new SwitchPortVlanConfiguration();
        config.ConfigureAsAccess(new VlanId(50));

        config.ConfigureAsTrunk();

        Assert.True(config.IsTrunk);
        Assert.Equal(VlanId.Default, config.AccessVlan);
        Assert.True(config.AllowsAllVlans);
    }

    [Fact]
    public void SetAccessVlan_IsIgnoredOnATrunk_And_SetNativeVlan_IsIgnoredOnAnAccessPort()
    {
        var config = new SwitchPortVlanConfiguration();

        config.SetNativeVlan(new VlanId(99)); // access port - ignored
        Assert.Equal(VlanId.Default, config.NativeVlan);

        config.ConfigureAsTrunk();
        config.SetAccessVlan(new VlanId(10)); // trunk - ignored
        Assert.Equal(VlanId.Default, config.AccessVlan);
    }
}
