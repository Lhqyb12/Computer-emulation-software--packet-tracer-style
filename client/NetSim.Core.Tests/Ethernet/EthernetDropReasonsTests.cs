using NetSim.Core.Ethernet;
using NetSim.Core.Packets;

namespace NetSim.Core.Tests.Ethernet;

public class EthernetDropReasonsTests
{
    private static readonly PacketDropReason[] All =
    [
        EthernetDropReasons.InvalidFrame,
        EthernetDropReasons.SourceInterfaceDown,
        EthernetDropReasons.DestinationInterfaceDown,
        EthernetDropReasons.NoPhysicalConnection,
        EthernetDropReasons.NotEthernetCapable,
        EthernetDropReasons.SourceMacMissing,
        EthernetDropReasons.SourceInterfaceNotInTopology,
        EthernetDropReasons.InvalidEndpoint,
    ];

    [Fact]
    public void EveryReason_HasADistinctEthPrefixedCode()
    {
        Assert.All(All, r => Assert.StartsWith("ETH_", r.Code));
        Assert.Equal(All.Length, All.Select(r => r.Code).Distinct().Count());
    }

    [Fact]
    public void Reasons_ArePlainPacketDropReasons_WithValueEquality()
    {
        PacketDropReason reason = EthernetDropReasons.InvalidFrame;

        Assert.IsType<PacketDropReason>(reason);
        Assert.Equal(new PacketDropReason("ETH_INVALID_FRAME", "The Ethernet frame failed structural validation."), reason);
        Assert.Equal("ETH_INVALID_FRAME: The Ethernet frame failed structural validation.", reason.ToString());
    }
}
