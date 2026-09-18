using NetSim.Core.Common.Exceptions;
using NetSim.Core.Networking;

namespace NetSim.Core.Tests.Networking;

public class SubnetMaskTests
{
    [Theory]
    [InlineData(0, "0.0.0.0")]
    [InlineData(8, "255.0.0.0")]
    [InlineData(16, "255.255.0.0")]
    [InlineData(24, "255.255.255.0")]
    [InlineData(25, "255.255.255.128")]
    [InlineData(26, "255.255.255.192")]
    [InlineData(27, "255.255.255.224")]
    [InlineData(28, "255.255.255.240")]
    [InlineData(29, "255.255.255.248")]
    [InlineData(30, "255.255.255.252")]
    [InlineData(31, "255.255.255.254")]
    [InlineData(32, "255.255.255.255")]
    public void FromPrefixLength_ProducesTheExpectedMask_AndConvertsBack(int prefix, string expectedMask)
    {
        var mask = SubnetMask.FromPrefixLength(prefix);

        Assert.Equal(expectedMask, mask.ToString());
        Assert.Equal(prefix, mask.PrefixLength);
        Assert.Equal(mask, SubnetMask.Parse(expectedMask));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(33)]
    [InlineData(64)]
    public void FromPrefixLength_RejectsOutOfRange(int prefix)
    {
        Assert.Throws<DomainException>(() => SubnetMask.FromPrefixLength(prefix));
    }

    [Theory]
    [InlineData("255.255.255.0")]
    [InlineData("255.255.255.128")]
    [InlineData("255.255.255.192")]
    [InlineData("255.255.0.0")]
    [InlineData("255.0.0.0")]
    [InlineData("0.0.0.0")]
    [InlineData("255.255.255.255")]
    public void Parse_AcceptsContiguousMasks(string input)
    {
        Assert.True(SubnetMask.TryParse(input, out var mask));
        Assert.Equal(input, mask.ToString());
    }

    [Theory]
    [InlineData("255.0.255.0")]
    [InlineData("255.255.0.255")]
    [InlineData("0.255.0.0")]
    [InlineData("255.255.1.0")]
    [InlineData("254.0.0.1")]
    [InlineData("256.255.255.0")]
    [InlineData("not-a-mask")]
    [InlineData("")]
    [InlineData(null)]
    public void Parse_RejectsNonContiguousOrMalformedMasks(string? input)
    {
        Assert.False(SubnetMask.TryParse(input, out _));
        Assert.Throws<DomainException>(() => SubnetMask.Parse(input));
    }

    [Fact]
    public void WildcardMask_IsTheInverse()
    {
        Assert.Equal(IPv4Address.Parse("0.0.0.255"), SubnetMask.Parse("255.255.255.0").WildcardMask);
        Assert.Equal(IPv4Address.Parse("0.0.0.63"), SubnetMask.FromPrefixLength(26).WildcardMask);
    }

    [Fact]
    public void Equality_IsValueBased()
    {
        Assert.Equal(SubnetMask.Parse("255.255.255.0"), SubnetMask.FromPrefixLength(24));
        Assert.NotEqual(SubnetMask.FromPrefixLength(24), SubnetMask.FromPrefixLength(25));
    }

    [Fact]
    public void Default_IsTheSlashZeroMask()
    {
        Assert.Equal(0, default(SubnetMask).PrefixLength);
        Assert.Equal(SubnetMask.None, default(SubnetMask));
    }

    [Fact]
    public void IsContiguous_MatchesTheStandardMasks()
    {
        Assert.True(SubnetMask.IsContiguous(0xFFFFFF00u));
        Assert.True(SubnetMask.IsContiguous(0u));
        Assert.True(SubnetMask.IsContiguous(0xFFFFFFFFu));
        Assert.False(SubnetMask.IsContiguous(0xFF00FF00u));
    }
}
