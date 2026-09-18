using NetSim.Core.Packets;

namespace NetSim.Core.Tests.Packets;

public class PacketProcessingResultTests
{
    [Fact]
    public void Transmitted_IsSuccess_WithNoDropReason()
    {
        var result = PacketProcessingResult.Transmitted("left G0/0");

        Assert.Equal(PacketProcessingOutcome.Transmitted, result.Outcome);
        Assert.True(result.IsSuccess);
        Assert.Null(result.DropReason);
        Assert.Equal("left G0/0", result.Detail);
    }

    [Fact]
    public void Delivered_IsSuccess()
    {
        Assert.True(PacketProcessingResult.Delivered().IsSuccess);
    }

    [Fact]
    public void Dropped_CarriesTheReason_AndIsNotSuccess()
    {
        var reason = new PacketDropReason("CUSTOM", "because");

        var result = PacketProcessingResult.Dropped(reason, "detail");

        Assert.Equal(PacketProcessingOutcome.Dropped, result.Outcome);
        Assert.False(result.IsSuccess);
        Assert.Same(reason, result.DropReason);
    }

    [Fact]
    public void Unsupported_DefaultsToUnsupportedProtocolReason()
    {
        var result = PacketProcessingResult.Unsupported("EtherType 0x9999");

        Assert.Equal(PacketProcessingOutcome.Unsupported, result.Outcome);
        Assert.False(result.IsSuccess);
        Assert.Same(PacketDropReason.UnsupportedProtocol, result.DropReason);
    }

    [Fact]
    public void Invalid_DefaultsToInvalidPacketReason()
    {
        var result = PacketProcessingResult.Invalid("truncated");

        Assert.Equal(PacketProcessingOutcome.Invalid, result.Outcome);
        Assert.Same(PacketDropReason.InvalidPacket, result.DropReason);
    }

    [Fact]
    public void Dropped_WithNullReason_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => PacketProcessingResult.Dropped(null!));
    }
}
