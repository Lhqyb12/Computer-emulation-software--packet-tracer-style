using NetSim.Core.Common.Exceptions;
using NetSim.Core.Devices;
using NetSim.Core.Packets;

namespace NetSim.Core.Tests.Packets;

public class PacketEngineTests
{
    private static (PacketEngine Engine, Pc Source, Pc Destination) NewEngine()
        => (new PacketEngine(), new Pc("PC0"), new Pc("PC1"));

    [Fact]
    public void CreatePacket_RegistersItAndRaisesPacketCreated()
    {
        var (engine, src, dst) = NewEngine();
        PacketEventArgs? raised = null;
        engine.PacketCreated += (_, e) => raised = e;

        var packet = engine.CreatePacket(src, dst, "Unspecified", RawPayload.OfSize(64));

        Assert.Contains(packet, engine.Packets);
        Assert.Contains(packet, engine.ActivePackets);
        Assert.NotNull(raised);
        Assert.Same(packet, raised!.Packet);
        Assert.Null(raised.Transition);
        Assert.Equal(PacketState.Created, packet.State);
    }

    [Fact]
    public void CreatePacket_WithStructurallyInvalidPayload_ThrowsAndDoesNotRegister()
    {
        var (engine, src, dst) = NewEngine();
        var badPayload = new StubPayload { ValidationResult = PacketValidationResult.Invalid("broken") };

        Assert.Throws<DomainException>(() => engine.CreatePacket(src, dst, "Unspecified", badPayload));
        Assert.Empty(engine.Packets);
    }

    [Fact]
    public void ActivePackets_ExcludesDeliveredAndDropped()
    {
        var (engine, src, dst) = NewEngine();
        var delivered = engine.CreatePacket(src, dst, "P");
        var dropped = engine.CreatePacket(src, dst, "P");
        var inFlight = engine.CreatePacket(src, dst, "P");

        engine.MarkTransmitted(delivered);
        engine.MarkDelivered(delivered);
        engine.Drop(dropped, PacketDropReason.InvalidPacket);
        engine.MarkTransmitted(inFlight);

        Assert.Equal(new[] { inFlight }, engine.ActivePackets);
        Assert.Equal(3, engine.Packets.Count);
    }

    [Fact]
    public void MarkTransitions_RaisePacketStateChanged_WithTheTransition()
    {
        var (engine, src, dst) = NewEngine();
        var packet = engine.CreatePacket(src, dst, "P");
        var transitions = new List<PacketStateTransition>();
        engine.PacketStateChanged += (_, e) => transitions.Add(e.Transition!.Value);

        engine.MarkTransmitted(packet, "G0/0");
        engine.MarkInTransit(packet);
        engine.MarkDelivered(packet, "G0/1");

        Assert.Equal(
            new[] { PacketState.Transmitted, PacketState.InTransit, PacketState.Delivered },
            transitions.Select(t => t.To));
        Assert.Equal("G0/0", transitions[0].Note);
    }

    [Fact]
    public void Process_TransmittedOutcome_MovesPacketToTransmitted_AndRaisesBothEvents()
    {
        var (engine, src, dst) = NewEngine();
        var packet = engine.CreatePacket(src, dst, "P");
        var processor = new StubProcessor(PacketProcessingResult.Transmitted("ok"));
        var stateChanged = 0;
        var processed = 0;
        engine.PacketStateChanged += (_, _) => stateChanged++;
        engine.PacketProcessed += (_, _) => processed++;

        var result = engine.Process(packet, processor);

        Assert.Equal(1, processor.ProcessCallCount);
        Assert.Same(packet, processor.LastPacket);
        Assert.Equal(PacketProcessingOutcome.Transmitted, result.Outcome);
        Assert.Equal(PacketState.Transmitted, packet.State);
        Assert.Equal(1, stateChanged);
        Assert.Equal(1, processed);
    }

    [Fact]
    public void Process_DeliveredOutcome_DrivesPacketThroughToDelivered()
    {
        var (engine, src, dst) = NewEngine();
        var packet = engine.CreatePacket(src, dst, "P");

        engine.Process(packet, new StubProcessor(PacketProcessingResult.Delivered()));

        Assert.Equal(PacketState.Delivered, packet.State);
        Assert.True(packet.HasCompleted);
    }

    [Fact]
    public void Process_DroppedOutcome_DropsPacketWithTheProcessorsReason()
    {
        var (engine, src, dst) = NewEngine();
        var packet = engine.CreatePacket(src, dst, "P");
        var reason = new PacketDropReason("L2_NO_LINK", "no physical connection");

        var result = engine.Process(packet, new StubProcessor(PacketProcessingResult.Dropped(reason)));

        Assert.Equal(PacketState.Dropped, packet.State);
        Assert.Same(reason, packet.DropReason);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Process_UnsupportedOutcome_DropsWithUnsupportedProtocolReason()
    {
        var (engine, src, dst) = NewEngine();
        var packet = engine.CreatePacket(src, dst, "P");

        engine.Process(packet, new StubProcessor(PacketProcessingResult.Unsupported("no IPv4 yet")));

        Assert.Equal(PacketState.Dropped, packet.State);
        Assert.Same(PacketDropReason.UnsupportedProtocol, packet.DropReason);
    }

    [Fact]
    public void Process_InvalidOutcome_DropsWithInvalidPacketReason()
    {
        var (engine, src, dst) = NewEngine();
        var packet = engine.CreatePacket(src, dst, "P");

        engine.Process(packet, new StubProcessor(PacketProcessingResult.Invalid("truncated")));

        Assert.Equal(PacketState.Dropped, packet.State);
        Assert.Same(PacketDropReason.InvalidPacket, packet.DropReason);
    }

    [Fact]
    public void Process_TerminalPacket_IsANoOpTransition_ButStillReportsProcessed()
    {
        var (engine, src, dst) = NewEngine();
        var packet = engine.CreatePacket(src, dst, "P");
        engine.Drop(packet, PacketDropReason.InvalidPacket);
        var processed = 0;
        engine.PacketProcessed += (_, _) => processed++;

        var result = engine.Process(packet, new StubProcessor(PacketProcessingResult.Delivered()));

        Assert.Equal(PacketState.Dropped, packet.State);
        Assert.Equal(PacketProcessingOutcome.Delivered, result.Outcome);
        Assert.Equal(1, processed);
    }

    [Fact]
    public void Reset_ClearsTrackedPackets()
    {
        var (engine, src, dst) = NewEngine();
        engine.CreatePacket(src, dst, "P");
        engine.CreatePacket(src, dst, "P");

        engine.Reset();

        Assert.Empty(engine.Packets);
        Assert.Empty(engine.ActivePackets);
    }

    [Fact]
    public void Process_NullArguments_Throw()
    {
        var (engine, src, dst) = NewEngine();
        var packet = engine.CreatePacket(src, dst, "P");

        Assert.Throws<ArgumentNullException>(() => engine.Process(null!, new StubProcessor(PacketProcessingResult.Delivered())));
        Assert.Throws<ArgumentNullException>(() => engine.Process(packet, null!));
    }
}
