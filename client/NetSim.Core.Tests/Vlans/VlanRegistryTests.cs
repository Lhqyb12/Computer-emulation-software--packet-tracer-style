using NetSim.Core.Common.Exceptions;
using NetSim.Core.Vlans;

namespace NetSim.Core.Tests.Vlans;

/// <summary>Phase 26 - the per-switch VLAN database: default VLAN, create, lookup, duplicate rejection, removal, naming.</summary>
public class VlanRegistryTests
{
    [Fact]
    public void ANewRegistry_ContainsOnlyTheDefaultVlan()
    {
        var registry = new VlanRegistry();

        var all = registry.GetAllVlans();
        Assert.Single(all);
        Assert.Equal(VlanId.Default, all[0].Id);
        Assert.Equal("default", all[0].Name);
        Assert.True(all[0].IsActive);
        Assert.True(registry.Exists(VlanId.Default));
    }

    [Fact]
    public void CreateVlan_AddsIt_AndRaisesVlanCreated()
    {
        var registry = new VlanRegistry();
        Vlan? created = null;
        registry.VlanCreated += (_, e) => created = e.Vlan;

        var vlan = registry.CreateVlan(new VlanId(10), "Students");

        Assert.Equal(new VlanId(10), vlan.Id);
        Assert.Equal("Students", vlan.Name);
        Assert.Same(vlan, registry.GetVlan(new VlanId(10)));
        Assert.Same(vlan, created);
        Assert.Equal([1, 10], registry.GetAllVlans().Select(v => v.Id.Value));
    }

    [Fact]
    public void CreateVlan_WithNoName_GetsTheCiscoStyleDefaultName()
    {
        var registry = new VlanRegistry();

        Assert.Equal("VLAN0010", registry.CreateVlan(new VlanId(10)).Name);
    }

    [Fact]
    public void CreateVlan_WithADuplicateId_Throws()
    {
        var registry = new VlanRegistry();
        registry.CreateVlan(new VlanId(10));

        Assert.Throws<DomainException>(() => registry.CreateVlan(new VlanId(10)));
        Assert.Throws<DomainException>(() => registry.CreateVlan(VlanId.Default));
    }

    [Fact]
    public void TryCreateVlan_ReturnsFalseForADuplicate_WithoutThrowing()
    {
        var registry = new VlanRegistry();
        Assert.True(registry.TryCreateVlan(new VlanId(10), out var first));
        Assert.False(registry.TryCreateVlan(new VlanId(10), out var second));
        Assert.Same(first, second);
    }

    [Fact]
    public void RemoveVlan_RemovesANonDefaultVlan_AndRaisesVlanRemoved()
    {
        var registry = new VlanRegistry();
        registry.CreateVlan(new VlanId(20));
        Vlan? removed = null;
        registry.VlanRemoved += (_, e) => removed = e.Vlan;

        Assert.True(registry.RemoveVlan(new VlanId(20)));
        Assert.False(registry.Exists(new VlanId(20)));
        Assert.Equal(new VlanId(20), removed!.Id);
    }

    [Fact]
    public void RemoveVlan_NeverRemovesTheDefaultVlan_AndReturnsFalseForAnUnknownVlan()
    {
        var registry = new VlanRegistry();

        Assert.False(registry.RemoveVlan(VlanId.Default));
        Assert.False(registry.RemoveVlan(new VlanId(99)));
        Assert.True(registry.Exists(VlanId.Default));
    }

    [Fact]
    public void Vlan_Rename_And_Activate_Deactivate_Work()
    {
        var registry = new VlanRegistry();
        var vlan = registry.CreateVlan(new VlanId(30), "Old");

        vlan.Rename("Management");
        Assert.Equal("Management", vlan.Name);
        Assert.Throws<DomainException>(() => vlan.Rename("   "));

        vlan.Deactivate();
        Assert.False(vlan.IsActive);
        vlan.Activate();
        Assert.True(vlan.IsActive);
    }
}
