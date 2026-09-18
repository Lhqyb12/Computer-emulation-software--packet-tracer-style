using NetSim.Core.Packets;

namespace NetSim.Core.Tests.Packets;

/// <summary>
/// Proves the <see cref="IPacketPayload"/> abstraction supports the layered encapsulation later
/// phases need (Ethernet -&gt; IPv4 -&gt; segment -&gt; application data) without any of those
/// protocol types existing yet - a stub outer layer wrapping a real <see cref="RawPayload"/> leaf.
/// </summary>
public class PacketPayloadEncapsulationTests
{
    [Fact]
    public void OuterPayload_ExposesInnerPayload()
    {
        var inner = RawPayload.OfSize(100);
        var outer = new StubPayload { PayloadType = "Outer", OwnLength = 14, EncapsulatedPayload = inner };

        Assert.Same(inner, outer.EncapsulatedPayload);
        Assert.Null(inner.EncapsulatedPayload);
    }

    [Fact]
    public void Length_AggregatesEveryLayer()
    {
        var leaf = RawPayload.OfSize(100);
        var middle = new StubPayload { OwnLength = 20, EncapsulatedPayload = leaf };
        var outer = new StubPayload { OwnLength = 14, EncapsulatedPayload = middle };

        Assert.Equal(134, outer.Length);
    }

    [Fact]
    public void MultiLayerPayload_RidesInsideAGenericPacket()
    {
        var frame = new StubPayload
        {
            PayloadType = "Frame",
            OwnLength = 14,
            EncapsulatedPayload = RawPayload.FromText("application data"),
        };

        var packet = new Packet(new NetSim.Core.Devices.Pc("A"), new NetSim.Core.Devices.Pc("B"), "L2", frame);

        Assert.Same(frame, packet.Payload);
        Assert.Equal("Frame", packet.Payload!.PayloadType);
        Assert.Equal("application data".Length + 14, packet.Payload.Length);
        Assert.True(packet.Validate().IsValid);
    }
}
