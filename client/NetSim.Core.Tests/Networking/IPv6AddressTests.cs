using NetSim.Core.Common.Exceptions;
using NetSim.Core.Networking;

namespace NetSim.Core.Tests.Networking;

public class IPv6AddressTests
{
    [Theory]
    [InlineData("::", "::")]
    [InlineData("::1", "::1")]
    [InlineData("fe80::1", "fe80::1")]
    [InlineData("2001:db8::1", "2001:db8::1")]
    [InlineData("2001:db8:1234:5678::1", "2001:db8:1234:5678::1")]
    [InlineData("ff02::1", "ff02::1")]
    [InlineData("2001:db8:0:0:1:0:0:1", "2001:db8::1:0:0:1")]
    [InlineData("2001:DB8::1", "2001:db8::1")]
    [InlineData("2001:0db8:0000:0000:0000:0000:0000:0001", "2001:db8::1")]
    [InlineData("1:2:3:4:5:6:7:8", "1:2:3:4:5:6:7:8")]
    public void Parse_AcceptsValidForms_AndCanonicalises(string input, string canonical)
    {
        var address = IPv6Address.Parse(input);

        Assert.Equal(canonical, address.ToString());
    }

    [Fact]
    public void Parse_ZeroCompression_MeansTheSameAddressAsTheFullForm()
    {
        Assert.Equal(
            IPv6Address.Parse("2001:0db8:0000:0000:0000:0000:0000:0001"),
            IPv6Address.Parse("2001:db8::1"));
    }

    [Theory]
    [InlineData("2001:db8:::1")]
    [InlineData("2001:db8:1:2:3:4:5:6:7")]
    [InlineData("2001:db8::1::2")]
    [InlineData("12345::1")]
    [InlineData("2001:db8:1:2:3:4:5")]
    [InlineData("gggg::1")]
    [InlineData("invalid text")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("fe80::1%eth0")]
    [InlineData("[::1]")]
    [InlineData(":1:2:3:4:5:6:7")]
    [InlineData("1:2:3:4:5:6:7:")]
    [InlineData("::1 ")]
    public void Parse_RejectsMalformedInput(string? input)
    {
        Assert.False(IPv6Address.TryParse(input, out _));
        Assert.Throws<DomainException>(() => IPv6Address.Parse(input));
    }

    [Fact]
    public void Equality_IsValueBased_AndFormatIndependent()
    {
        var a = IPv6Address.Parse("2001:db8::1");
        var b = IPv6Address.Parse("2001:0db8:0:0:0:0:0:1");
        var different = IPv6Address.Parse("2001:db8::2");

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.NotEqual(a, different);
    }

    [Fact]
    public void GetHashCode_MatchesForEqualAddresses()
    {
        Assert.Equal(
            IPv6Address.Parse("fe80::abcd").GetHashCode(),
            IPv6Address.Parse("fe80:0:0:0:0:0:0:abcd").GetHashCode());
    }

    [Fact]
    public void GetBytes_And_FromBytes_RoundTrip_MostSignificantByteFirst()
    {
        var address = IPv6Address.Parse("2001:db8::1");
        var bytes = address.GetBytes();

        Assert.Equal(16, bytes.Length);
        Assert.Equal(0x20, bytes[0]);
        Assert.Equal(0x01, bytes[1]);
        Assert.Equal(0x0d, bytes[2]);
        Assert.Equal(0xb8, bytes[3]);
        Assert.Equal(0x01, bytes[15]);
        Assert.Equal(address, IPv6Address.FromBytes(bytes));
    }

    [Fact]
    public void FromBytes_RejectsWrongLength()
    {
        Assert.Throws<DomainException>(() => IPv6Address.FromBytes(new byte[15]));
        Assert.Throws<DomainException>(() => IPv6Address.FromBytes(new byte[17]));
    }

    [Fact]
    public void Constants_AndDefault_Match()
    {
        Assert.Equal(IPv6Address.Parse("::"), IPv6Address.Unspecified);
        Assert.Equal(IPv6Address.Parse("::"), default(IPv6Address));
        Assert.Equal(IPv6Address.Parse("::1"), IPv6Address.Loopback);
        Assert.Equal("::", IPv6Address.Unspecified.ToString());
        Assert.Equal("::1", IPv6Address.Loopback.ToString());
    }

    [Fact]
    public void Comparison_OrdersNumerically()
    {
        var low = IPv6Address.Parse("2001:db8::1");
        var high = IPv6Address.Parse("2001:db8::2");

        Assert.True(low < high);
        Assert.True(high > low);
        Assert.True(low <= IPv6Address.Parse("2001:db8::1"));
        Assert.True(low.CompareTo(high) < 0);
    }

    [Fact]
    public void ToString_DoesNotCompressASingleZeroGroup()
    {
        Assert.Equal("2001:db8:0:1:1:1:1:1", IPv6Address.Parse("2001:db8:0:1:1:1:1:1").ToString());
    }

    [Fact]
    public void ToString_CompressesTheLongestZeroRun_LeftmostOnATie()
    {
        // Two runs of length two - the leftmost is compressed.
        Assert.Equal("1:0:0:1::1", IPv6Address.Parse("1:0:0:1:0:0:0:1").ToString());
        Assert.Equal("2001:db8::1:0:0:1", IPv6Address.Parse("2001:db8:0:0:1:0:0:1").ToString());
    }

    [Theory]
    [InlineData("::", IPv6AddressCategory.Unspecified)]
    [InlineData("::1", IPv6AddressCategory.Loopback)]
    [InlineData("fe80::1", IPv6AddressCategory.LinkLocal)]
    [InlineData("febf::1", IPv6AddressCategory.LinkLocal)]
    [InlineData("fc00::1", IPv6AddressCategory.UniqueLocal)]
    [InlineData("fd12:3456:789a::1", IPv6AddressCategory.UniqueLocal)]
    [InlineData("ff02::1", IPv6AddressCategory.Multicast)]
    [InlineData("ff00::", IPv6AddressCategory.Multicast)]
    [InlineData("2001:db8::1", IPv6AddressCategory.GlobalUnicast)]
    [InlineData("2000::", IPv6AddressCategory.GlobalUnicast)]
    [InlineData("3fff::1", IPv6AddressCategory.GlobalUnicast)]
    [InlineData("::ffff:192.168.1.1", IPv6AddressCategory.Other)]
    [InlineData("100::1", IPv6AddressCategory.Other)]
    public void Category_ClassifiesSpecialRanges(string input, IPv6AddressCategory expected)
    {
        Assert.Equal(expected, IPv6Address.Parse(input).Category);
    }

    [Fact]
    public void ClassificationFlags_AreConsistentWithCategory()
    {
        Assert.True(IPv6Address.Parse("::").IsUnspecified);
        Assert.True(IPv6Address.Parse("::1").IsLoopback);
        Assert.True(IPv6Address.Parse("fe80::1").IsLinkLocal);
        Assert.True(IPv6Address.Parse("fc00::1").IsUniqueLocal);
        Assert.True(IPv6Address.Parse("ff02::1").IsMulticast);
        Assert.True(IPv6Address.Parse("2001:db8::1").IsGlobalUnicast);
        Assert.False(IPv6Address.Parse("2001:db8::1").IsLinkLocal);
    }

    [Fact]
    public void Parse_AcceptsAnEmbeddedIPv4Tail()
    {
        var address = IPv6Address.Parse("::ffff:192.168.1.1");

        Assert.Equal(IPv6Address.Parse("::ffff:c0a8:101"), address);
    }
}
