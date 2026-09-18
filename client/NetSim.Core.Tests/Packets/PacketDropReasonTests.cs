using NetSim.Core.Packets;

namespace NetSim.Core.Tests.Packets;

public class PacketDropReasonTests
{
    [Fact]
    public void ValueEquality_MatchesOnCodeAndDescription()
    {
        var a = new PacketDropReason("X", "same");
        var b = new PacketDropReason("X", "same");
        var c = new PacketDropReason("X", "different");

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void GenericReasons_HaveDistinctStableCodes()
    {
        var codes = new[]
        {
            PacketDropReason.InvalidPacket.Code,
            PacketDropReason.InvalidPayload.Code,
            PacketDropReason.UnsupportedProtocol.Code,
        };

        Assert.Equal(codes.Length, codes.Distinct().Count());
        Assert.Equal("INVALID_PACKET", PacketDropReason.InvalidPacket.Code);
    }

    [Fact]
    public void ToString_IsCodeThenDescription()
    {
        Assert.Equal("INVALID_PACKET: The packet failed structural validation.", PacketDropReason.InvalidPacket.ToString());
    }
}
