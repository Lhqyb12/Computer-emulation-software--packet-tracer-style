using System.Linq;
using NetSim.Core.Icmp;

namespace NetSim.Core.Tests.Icmp;

public class IcmpDropReasonsTests
{
    [Fact]
    public void EachReason_HasADistinctIcmpPrefixedCode()
    {
        var codes = new[] { IcmpDropReasons.NotIcmp.Code, IcmpDropReasons.InvalidPacket.Code, IcmpDropReasons.InvalidChecksum.Code };

        Assert.All(codes, code => Assert.StartsWith("ICMP_", code));
        Assert.Equal(codes.Length, codes.Distinct().Count());
    }

    [Fact]
    public void Reasons_ArePlainPacketDropReasonValues()
    {
        Assert.Equal(
            new NetSim.Core.Packets.PacketDropReason("ICMP_NOT_ICMP", IcmpDropReasons.NotIcmp.Description),
            IcmpDropReasons.NotIcmp);
    }
}
