using NetSim.Core.IP;
using NetSim.Core.Packets;

namespace NetSim.Core.Tests.IP;

public class IPv4DropReasonsTests
{
    [Fact]
    public void EveryReason_HasADistinctIpv4PrefixedCode()
    {
        var reasons = new[]
        {
            IPv4DropReasons.NotIPv4,
            IPv4DropReasons.InvalidPacket,
            IPv4DropReasons.TimeToLiveExpired,
            IPv4DropReasons.InvalidSourceAddress,
            IPv4DropReasons.InvalidDestinationAddress,
        };

        Assert.All(reasons, r => Assert.StartsWith("IPV4_", r.Code));
        Assert.Equal(reasons.Length, reasons.Select(r => r.Code).Distinct().Count());
    }

    [Fact]
    public void Reasons_ArePlainPacketDropReasonValues_WithValueEquality()
    {
        Assert.IsType<PacketDropReason>(IPv4DropReasons.TimeToLiveExpired);
        Assert.Equal(IPv4DropReasons.TimeToLiveExpired, IPv4DropReasons.TimeToLiveExpired);
        Assert.NotEqual(IPv4DropReasons.InvalidPacket, IPv4DropReasons.NotIPv4);
    }
}
