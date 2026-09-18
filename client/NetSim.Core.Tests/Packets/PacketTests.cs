using NetSim.Core.Common.Exceptions;
using NetSim.Core.Devices;
using NetSim.Core.Packets;

namespace NetSim.Core.Tests.Packets;

public class PacketTests
{
    private static Packet NewPacket(IPacketPayload? payload = null) =>
        new(new Pc("PC1"), new Pc("PC2"), "Unspecified", payload);

    [Fact]
    public void Constructor_SetsSourceDestinationAndProtocol()
    {
        var pc1 = new Pc("PC1");
        var pc2 = new Pc("PC2");

        var packet = new Packet(pc1, pc2, "Unspecified");

        Assert.Same(pc1, packet.Source);
        Assert.Same(pc2, packet.Destination);
        Assert.Equal("Unspecified", packet.Protocol);
        Assert.NotEqual(default, packet.Id);
    }

    [Fact]
    public void TwoPackets_HaveDifferentIds()
    {
        var pc1 = new Pc("PC1");
        var pc2 = new Pc("PC2");

        var first = new Packet(pc1, pc2, "Unspecified");
        var second = new Packet(pc1, pc2, "Unspecified");

        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void NewPacket_StartsInCreatedState_WithNoHistoryOrDropReason()
    {
        var packet = NewPacket();

        Assert.Equal(PacketState.Created, packet.State);
        Assert.Empty(packet.StateHistory);
        Assert.Null(packet.DropReason);
        Assert.False(packet.IsInFlight);
        Assert.False(packet.HasCompleted);
    }

    [Fact]
    public void NewPacket_StartsAtItsSourceDevice_WithNoHops()
    {
        var source = new Pc("PC1");
        var packet = new Packet(source, new Pc("PC2"), "Unspecified");

        Assert.Equal(PacketLocationKind.AtDevice, packet.Location.Kind);
        Assert.Same(source, packet.Location.Device);
        Assert.Same(source, packet.CurrentDevice);
        Assert.Null(packet.CurrentInterface);
        Assert.Empty(packet.Hops);
        Assert.Equal(0, packet.HopCount);
    }

    [Fact]
    public void MarkDelivered_MovesLocationToTheDeliveredTerminal()
    {
        var packet = NewPacket();
        packet.MarkTransmitted();

        packet.MarkDelivered();

        Assert.Equal(PacketLocationKind.Delivered, packet.Location.Kind);
        Assert.Null(packet.CurrentDevice);
    }

    [Fact]
    public void Drop_MovesLocationToTheDroppedTerminal()
    {
        var packet = NewPacket();

        packet.Drop(PacketDropReason.InvalidPacket);

        Assert.Equal(PacketLocationKind.Dropped, packet.Location.Kind);
    }

    [Fact]
    public void Constructor_StoresPayload()
    {
        var payload = RawPayload.FromText("hello");

        var packet = NewPacket(payload);

        Assert.Same(payload, packet.Payload);
    }

    [Fact]
    public void Constructor_RejectsBlankProtocol()
    {
        Assert.Throws<DomainException>(() => new Packet(new Pc("A"), new Pc("B"), "   "));
    }

    [Fact]
    public void Lifecycle_HappyPath_RecordsEveryTransition()
    {
        var packet = NewPacket();

        packet.MarkTransmitted("G0/0");
        packet.MarkInTransit();
        packet.MarkDelivered("G0/1");

        Assert.Equal(PacketState.Delivered, packet.State);
        Assert.True(packet.IsDelivered);
        Assert.True(packet.HasCompleted);
        Assert.Collection(
            packet.StateHistory,
            t => Assert.Equal((PacketState.Created, PacketState.Transmitted), (t.From, t.To)),
            t => Assert.Equal((PacketState.Transmitted, PacketState.InTransit), (t.From, t.To)),
            t => Assert.Equal((PacketState.InTransit, PacketState.Delivered), (t.From, t.To)));
        Assert.Equal("G0/0", packet.StateHistory[0].Note);
    }

    [Fact]
    public void MarkInTransit_FromCreated_Throws()
    {
        var packet = NewPacket();

        Assert.Throws<DomainException>(() => packet.MarkInTransit());
    }

    [Fact]
    public void MarkDelivered_DirectlyFromTransmitted_IsAllowed()
    {
        var packet = NewPacket();
        packet.MarkTransmitted();

        packet.MarkDelivered();

        Assert.Equal(PacketState.Delivered, packet.State);
    }

    [Theory]
    [InlineData(PacketState.Created)]
    [InlineData(PacketState.Transmitted)]
    [InlineData(PacketState.InTransit)]
    public void Drop_IsReachableFromEveryNonTerminalState(PacketState from)
    {
        var packet = NewPacket();
        if (from is PacketState.Transmitted or PacketState.InTransit)
        {
            packet.MarkTransmitted();
        }

        if (from is PacketState.InTransit)
        {
            packet.MarkInTransit();
        }

        packet.Drop(PacketDropReason.InvalidPacket, "test");

        Assert.Equal(PacketState.Dropped, packet.State);
        Assert.True(packet.IsDropped);
        Assert.Same(PacketDropReason.InvalidPacket, packet.DropReason);
    }

    [Fact]
    public void TerminalState_IsImmutable()
    {
        var delivered = NewPacket();
        delivered.MarkTransmitted();
        delivered.MarkDelivered();

        Assert.Throws<DomainException>(() => delivered.MarkTransmitted());
        Assert.Throws<DomainException>(() => delivered.Drop(PacketDropReason.InvalidPacket));

        var dropped = NewPacket();
        dropped.Drop(PacketDropReason.InvalidPacket);

        Assert.Throws<DomainException>(() => dropped.MarkTransmitted());
    }

    [Fact]
    public void Drop_WithNullReason_Throws()
    {
        var packet = NewPacket();

        Assert.Throws<ArgumentNullException>(() => packet.Drop(null!));
    }

    [Fact]
    public void Validate_PacketWithoutPayload_IsValid()
    {
        Assert.True(NewPacket().Validate().IsValid);
    }

    [Fact]
    public void Validate_PacketWithValidPayload_IsValid()
    {
        Assert.True(NewPacket(RawPayload.OfSize(64)).Validate().IsValid);
    }

    [Fact]
    public void Validate_PacketWithInvalidPayload_SurfacesPayloadErrors()
    {
        var payload = new StubPayload { ValidationResult = PacketValidationResult.Invalid("bad header") };

        var result = NewPacket(payload).Validate();

        Assert.False(result.IsValid);
        Assert.Contains("bad header", result.Errors);
    }
}
