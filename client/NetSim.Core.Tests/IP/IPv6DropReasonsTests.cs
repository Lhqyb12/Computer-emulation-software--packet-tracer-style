using NetSim.Core.IP;
using NetSim.Core.Packets;

namespace NetSim.Core.Tests.IP;

public class IPv6DropReasonsTests
{
    [Fact]
    public void EveryReason_HasADistinctIpv6PrefixedCode()
    {
        var reasons = new[]
        {
            IPv6DropReasons.NotIPv6,
            IPv6DropReasons.InvalidPacket,
            IPv6DropReasons.HopLimitExpired,
            IPv6DropReasons.InvalidSourceAddress,
            IPv6DropReasons.InvalidDestinationAddress,
        };

        Assert.All(reasons, r => Assert.StartsWith("IPV6_", r.Code));
        Assert.Equal(reasons.Length, reasons.Select(r => r.Code).Distinct().Count());
    }

    [Fact]
    public void Reasons_ArePlainPacketDropReasonValues_WithValueEquality()
    {
        Assert.IsType<PacketDropReason>(IPv6DropReasons.HopLimitExpired);
        Assert.Equal(IPv6DropReasons.HopLimitExpired, IPv6DropReasons.HopLimitExpired);
        Assert.NotEqual(IPv6DropReasons.InvalidPacket, IPv6DropReasons.NotIPv6);
    }
}
