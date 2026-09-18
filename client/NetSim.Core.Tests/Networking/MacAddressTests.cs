using NetSim.Core.Common.Exceptions;
using NetSim.Core.Networking;

namespace NetSim.Core.Tests.Networking;

public class MacAddressTests
{
    [Theory]
    [InlineData("00:1A:2B:3C:4D:5E")]
    [InlineData("00-1A-2B-3C-4D-5E")]
    [InlineData("001A.2B3C.4D5E")]
    [InlineData("001A2B3C4D5E")]
    [InlineData("00:1a:2b:3c:4d:5e")]
    public void Parse_AcceptsEverySupportedFormat_AndNormalises(string input)
    {
        var mac = MacAddress.Parse(input);

        Assert.Equal("00:1A:2B:3C:4D:5E", mac.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("00:1A:2B:3C:4D")]        // only five octets
    [InlineData("00:1A:2B:3C:4D:ZZ")]     // non-hex
    [InlineData("123")]
    [InlineData("00:1A:2B:3C:4D:5E:6F")]  // seven octets
    [InlineData("00:1A:2B:3C:4D:5E1")]    // thirteen hex digits
    public void Parse_RejectsInvalidInput(string? input)
    {
        Assert.False(MacAddress.TryParse(input, out _));
        Assert.Throws<DomainException>(() => MacAddress.Parse(input));
    }

    [Fact]
    public void Equality_IsValueBased_AndFormatIndependent()
    {
        var a = MacAddress.Parse("00:1A:2B:3C:4D:5E");
        var b = MacAddress.Parse("00-1A-2B-3C-4D-5E");
        var c = MacAddress.Parse("001A.2B3C.4D5E");
        var different = MacAddress.Parse("00:1A:2B:3C:4D:5F");

        Assert.Equal(a, b);
        Assert.Equal(a, c);
        Assert.True(a == b);
        Assert.False(a == different);
        Assert.NotEqual(a, different);
    }

    [Fact]
    public void GetHashCode_MatchesForEqualAddresses_AcrossFormats()
    {
        var a = MacAddress.Parse("AA:BB:CC:DD:EE:FF");
        var b = MacAddress.Parse("aabbccddeeff");

        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Broadcast_IsRecognisedAndClassified()
    {
        var fromConstant = MacAddress.Broadcast;
        var fromParse = MacAddress.Parse("FF:FF:FF:FF:FF:FF");

        Assert.Equal(fromConstant, fromParse);
        Assert.True(fromParse.IsBroadcast);
        Assert.False(fromParse.IsMulticast);
        Assert.False(fromParse.IsUnicast);
        Assert.Equal(MacAddressKind.Broadcast, fromParse.Kind);
        Assert.Equal("FF:FF:FF:FF:FF:FF", fromParse.ToString());
    }

    [Fact]
    public void Multicast_IsTheIgBitButNotBroadcast()
    {
        // 01:00:5E:... - IPv4 multicast OUI, I/G bit set.
        var multicast = MacAddress.Parse("01:00:5E:00:00:FB");

        Assert.True(multicast.IsMulticast);
        Assert.False(multicast.IsBroadcast);
        Assert.False(multicast.IsUnicast);
        Assert.Equal(MacAddressKind.Multicast, multicast.Kind);
    }

    [Fact]
    public void Unicast_IsTheClearedIgBit()
    {
        var unicast = MacAddress.Parse("00:1A:2B:3C:4D:5E");

        Assert.True(unicast.IsUnicast);
        Assert.False(unicast.IsMulticast);
        Assert.False(unicast.IsBroadcast);
        Assert.Equal(MacAddressKind.Unicast, unicast.Kind);
    }

    [Theory]
    [InlineData("02:00:00:00:00:01", true)]
    [InlineData("00:1A:2B:3C:4D:5E", false)]
    public void LocallyAdministered_ReflectsTheUlBit(string input, bool expected)
    {
        var mac = MacAddress.Parse(input);

        Assert.Equal(expected, mac.IsLocallyAdministered);
        Assert.Equal(!expected, mac.IsUniversallyAdministered);
    }

    [Fact]
    public void GetBytes_ReturnsSixOctetsMostSignificantFirst()
    {
        var mac = MacAddress.Parse("00:1A:2B:3C:4D:5E");

        Assert.Equal(new byte[] { 0x00, 0x1A, 0x2B, 0x3C, 0x4D, 0x5E }, mac.GetBytes());
    }

    [Fact]
    public void CreateRandomUnicast_IsAlwaysAValidLocallyAdministeredUnicastAddress()
    {
        for (var i = 0; i < 200; i++)
        {
            var mac = MacAddress.CreateRandomUnicast();

            Assert.True(mac.IsUnicast);
            Assert.False(mac.IsMulticast);
            Assert.False(mac.IsBroadcast);
            Assert.True(mac.IsLocallyAdministered);
            Assert.True(MacAddress.TryParse(mac.ToString(), out var roundTripped));
            Assert.Equal(mac, roundTripped);
        }
    }

    [Fact]
    public void CreateRandomUnicast_ProducesDistinctAddresses()
    {
        var generated = Enumerable.Range(0, 100).Select(_ => MacAddress.CreateRandomUnicast()).ToHashSet();

        // 46 random bits - a collision in 100 draws would be a bug, not bad luck.
        Assert.Equal(100, generated.Count);
    }
}
