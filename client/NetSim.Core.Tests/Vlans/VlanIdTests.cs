using NetSim.Core.Common.Exceptions;
using NetSim.Core.Vlans;

namespace NetSim.Core.Tests.Vlans;

/// <summary>Phase 26 - the VLAN id value object: the 802.1Q 1-4094 range and its edges.</summary>
public class VlanIdTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(4094)]
    public void Construct_WithAnInRangeValue_Succeeds(int value)
    {
        var vlan = new VlanId(value);
        Assert.Equal(value, vlan.Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(4095)]
    [InlineData(5000)]
    [InlineData(int.MaxValue)]
    public void Construct_WithAnOutOfRangeValue_Throws(int value)
    {
        Assert.Throws<DomainException>(() => new VlanId(value));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(4094, true)]
    [InlineData(4095, false)]
    public void IsValid_MatchesTheRange(int value, bool expected)
    {
        Assert.Equal(expected, VlanId.IsValid(value));
    }

    [Fact]
    public void TryCreate_ReturnsFalseForAnInvalidValue_AndTrueForAValidOne()
    {
        Assert.False(VlanId.TryCreate(0, out _));
        Assert.False(VlanId.TryCreate(4095, out _));
        Assert.True(VlanId.TryCreate(10, out var vlan));
        Assert.Equal(10, vlan.Value);
    }

    [Fact]
    public void Default_IsVlanOne()
    {
        Assert.Equal(1, VlanId.Default.Value);
        Assert.Equal(new VlanId(1), VlanId.Default);
    }

    [Fact]
    public void Equality_And_Ordering_AreByValue()
    {
        Assert.Equal(new VlanId(10), new VlanId(10));
        Assert.NotEqual(new VlanId(10), new VlanId(20));

        var sorted = new[] { new VlanId(30), new VlanId(1), new VlanId(20) }.OrderBy(v => v).ToList();
        Assert.Equal([1, 20, 30], sorted.Select(v => v.Value));
    }

    [Fact]
    public void ToString_IsTheBareNumber()
    {
        Assert.Equal("10", new VlanId(10).ToString());
    }
}
