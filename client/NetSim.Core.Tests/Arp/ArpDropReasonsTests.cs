using NetSim.Core.Arp;

namespace NetSim.Core.Tests.Arp;

public class ArpDropReasonsTests
{
    [Fact]
    public void EveryReason_HasADistinctArpPrefixedCode()
    {
        var codes = new[]
        {
            ArpDropReasons.NotArp.Code,
            ArpDropReasons.InvalidPacket.Code,
            ArpDropReasons.UnsupportedOperation.Code,
            ArpDropReasons.InvalidSenderInterface.Code,
        };

        Assert.All(codes, c => Assert.StartsWith("ARP_", c));
        Assert.Equal(codes.Length, codes.Distinct().Count());
    }

    [Fact]
    public void Reasons_ArePlainPacketDropReasons()
    {
        Assert.IsType<NetSim.Core.Packets.PacketDropReason>(ArpDropReasons.InvalidPacket);
    }
}
