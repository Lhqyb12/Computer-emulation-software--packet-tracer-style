using NetSim.Core.Common;
using NetSim.Core.Devices;
using NetSim.Core.Packets;

namespace NetSim.Core.Tests.Packets;

public class PacketRegistryTests
{
    private static (PacketEngine Engine, Pc A, Pc B) New() => (new PacketEngine(), new Pc("A"), new Pc("B"));

    [Fact]
    public void CreatePacket_AddsItToTheRegistry_LookupByIdIsAvailable()
    {
        var (engine, a, b) = New();

        var packet = engine.CreatePacket(a, b, "P");

        Assert.True(engine.Contains(packet.Id));
        Assert.Same(packet, engine.Get(packet.Id));
        Assert.True(engine.TryGet(packet.Id, out var viaTryGet));
        Assert.Same(packet, viaTryGet);
    }

    [Fact]
    public void Get_UnknownId_ReturnsNull_TryGet_ReturnsFalse()
    {
        var (engine, _, _) = New();

        Assert.Null(engine.Get(EntityId.New()));
        Assert.False(engine.Contains(EntityId.New()));
        Assert.False(engine.TryGet(EntityId.New(), out _));
    }

    [Fact]
    public void Remove_TakesThePacketOutOfEveryView()
    {
        var (engine, a, b) = New();
        var packet = engine.CreatePacket(a, b, "P");

        Assert.True(engine.Remove(packet.Id));

        Assert.False(engine.Contains(packet.Id));
        Assert.DoesNotContain(packet, engine.Packets);
        Assert.DoesNotContain(packet, engine.ActivePackets);
        Assert.False(engine.Remove(packet.Id)); // second remove is a no-op
    }

    [Fact]
    public void ActivePackets_AndTerminalPackets_PartitionTheRegistry()
    {
        var (engine, a, b) = New();
        var delivered = engine.CreatePacket(a, b, "P");
        var dropped = engine.CreatePacket(a, b, "P");
        var inFlight = engine.CreatePacket(a, b, "P");

        engine.MarkTransmitted(delivered);
        engine.MarkDelivered(delivered);
        engine.Drop(dropped, PacketDropReason.InvalidPacket);

        Assert.Equal(new[] { inFlight }, engine.ActivePackets);
        Assert.Equal(new[] { delivered, dropped }, engine.TerminalPackets);
        Assert.Equal(3, engine.Packets.Count);
    }

    [Fact]
    public void Reset_ClearsTheIdIndexToo()
    {
        var (engine, a, b) = New();
        var packet = engine.CreatePacket(a, b, "P");

        engine.Reset();

        Assert.False(engine.Contains(packet.Id));
        Assert.Empty(engine.Packets);
        Assert.Empty(engine.TerminalPackets);
    }
}
