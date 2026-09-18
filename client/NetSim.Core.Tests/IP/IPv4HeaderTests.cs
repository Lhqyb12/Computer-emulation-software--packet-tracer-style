using NetSim.Core.Common.Exceptions;
using NetSim.Core.IP;
using NetSim.Core.Networking;
using NetSim.Core.Packets;

namespace NetSim.Core.Tests.IP;

/// <summary>
/// The header is built by <see cref="IPv4Packet"/> (its constructor is internal), so these tests
/// exercise it through <see cref="IPv4Packet.Create"/> and inspect <see cref="IPv4Packet.Header"/>.
/// </summary>
public class IPv4HeaderTests
{
    private static readonly IPv4Address Src = IPv4Address.Parse("192.168.1.10");
    private static readonly IPv4Address Dst = IPv4Address.Parse("192.168.1.20");

    private static IPv4Header HeaderOf(
        IPacketPayload? payload = null,
        ProtocolNumber protocol = default,
        byte ttl = 64,
        ushort identification = 0,
        IPv4Flags flags = IPv4Flags.None,
        int fragmentOffset = 0) =>
        IPv4Packet.Create(Src, Dst, payload, protocol, ttl, identification, flags, fragmentOffset).Header;

    [Fact]
    public void FixedFields_AreVersion4AndA20ByteHeader()
    {
        var header = HeaderOf();

        Assert.Equal(4, IPv4Header.Version);
        Assert.Equal(20, header.HeaderLengthBytes);
        Assert.Equal(5, header.InternetHeaderLength);
    }

    [Fact]
    public void TotalLength_IsHeaderPlusPayload()
    {
        var header = HeaderOf(RawPayload.OfSize(100));

        Assert.Equal(120, header.TotalLengthBytes);
        Assert.Equal(100, header.PayloadLengthBytes);
    }

    [Fact]
    public void HeaderCarriesAllTheConfiguredFields()
    {
        var header = HeaderOf(
            RawPayload.OfSize(8),
            ProtocolNumber.Tcp,
            ttl: 55,
            identification: 4321,
            flags: IPv4Flags.DontFragment | IPv4Flags.MoreFragments,
            fragmentOffset: 128);

        Assert.Equal(Src, header.SourceAddress);
        Assert.Equal(Dst, header.DestinationAddress);
        Assert.Equal(ProtocolNumber.Tcp, header.Protocol);
        Assert.Equal(55, header.TimeToLive);
        Assert.Equal(4321, header.Identification);
        Assert.True(header.DontFragment);
        Assert.True(header.MoreFragments);
        Assert.Equal(128, header.FragmentOffset);
    }

    [Theory]
    [InlineData(8192)]
    [InlineData(-1)]
    public void Create_RejectsAnOutOfRangeFragmentOffset(int fragmentOffset)
    {
        Assert.Throws<DomainException>(() => HeaderOf(fragmentOffset: fragmentOffset));
    }

    [Fact]
    public void Validate_IsValid_ForAWellFormedHeader()
    {
        Assert.True(HeaderOf(RawPayload.OfSize(40)).Validate().IsValid);
    }

    [Fact]
    public void ToBytes_LaysOutTheStandard20ByteHeader_WithZeroedChecksum()
    {
        // Worked example from RFC 791 / the Wikipedia IPv4 header checksum example.
        var header = IPv4Packet.Create(
            IPv4Address.Parse("192.168.0.1"),
            IPv4Address.Parse("192.168.0.199"),
            RawPayload.OfSize(95),                 // total length 20 + 95 = 115
            ProtocolNumber.Udp,
            timeToLive: 64,
            identification: 0,
            flags: IPv4Flags.DontFragment).Header;

        var bytes = header.ToBytes();

        Assert.Equal(20, bytes.Length);
        Assert.Equal(new byte[]
        {
            0x45, 0x00, 0x00, 0x73, 0x00, 0x00, 0x40, 0x00, 0x40, 0x11,
            0x00, 0x00, // checksum field left zero
            0xC0, 0xA8, 0x00, 0x01, 0xC0, 0xA8, 0x00, 0xC7,
        }, bytes);
    }

    [Fact]
    public void ComputeChecksum_MatchesTheKnownRfc791Example()
    {
        var header = IPv4Packet.Create(
            IPv4Address.Parse("192.168.0.1"),
            IPv4Address.Parse("192.168.0.199"),
            RawPayload.OfSize(95),
            ProtocolNumber.Udp,
            timeToLive: 64,
            identification: 0,
            flags: IPv4Flags.DontFragment).Header;

        Assert.Equal(0xB861, header.ComputeChecksum());
    }
}
